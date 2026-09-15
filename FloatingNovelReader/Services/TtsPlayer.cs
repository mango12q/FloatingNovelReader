using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// mp3 片段播放器（NAudio）。
///
/// 链路按 edge-tts-plan.md §5 的**修正版**实现：
///   Mp3Frame.LoadFromStream → AcmMp3FrameDecompressor → BufferedWaveProvider → WaveOutEvent
/// 注意：
///   - 不存在 Mp3Frame.FromBytes / Mp3FileReader.CreateDecompressor（原稿写错的两个 API）；
///   - Mp3FileReader 构造时会扫描整个流，不能用于流式，这里不用；
///   - 不需要 WaveFormatConversionStream，解压器输出的已经是 PCM；
///   - 每段用**新的** WaveOutEvent，避免"Stop 之后重新 Init"的已知坑。
///
/// "这段播完了"用两条路判定（实测 PlaybackStopped 并不总会按预期触发）：
///   1. WaveOutEvent.PlaybackStopped；
///   2. 按 PCM 时长推算的兜底定时器。
/// 两条路都幂等，谁先到算谁。
/// </summary>
public sealed class TtsPlayer : IDisposable
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private Playback? _current;

    /// <summary>一段音频播完（Stop() 主动停止不会触发）。</summary>
    public event EventHandler? Finished;

    public bool IsPlaying
    {
        get { lock (_gate) return _output is { PlaybackState: PlaybackState.Playing }; }
    }

    /// <summary>播放一整段 mp3；播完触发 <see cref="Finished"/>。</summary>
    /// <param name="volume">0.0 – 1.0</param>
    public void PlaySegment(byte[] mp3, double volume)
    {
        Stop();

        var (samples, format) = Decode(mp3);
        if (format is null || samples.Length == 0)
        {
            Log.Warning("mp3 片段解码后没有 PCM 数据（{Bytes} 字节输入）", mp3.Length);
            // 空片段也要立刻报告结束，否则调用方会一直等
            Finished?.Invoke(this, EventArgs.Empty);
            return;
        }

        // BufferedWaveProvider 在缓冲耗尽时 Read 返回 0；默认缓冲只有 5 秒，
        // 整段 PCM 直接塞会抛 "Buffer full"，所以按实际长度开缓冲。
        var provider = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = false,
            BufferLength = samples.Length + format.AverageBytesPerSecond,
        };
        provider.AddSamples(samples, 0, samples.Length);

        var playback = new Playback();
        var seconds = samples.Length / (double)format.AverageBytesPerSecond;

        var output = new WaveOutEvent { Volume = (float)Math.Clamp(volume, 0.0, 1.0) };
        output.PlaybackStopped += (s, e) =>
        {
            if (IsCurrent(playback))
            {
                Log.Debug("片段播放结束（PlaybackStopped 路径）");
                Complete(playback);
            }
            else
            {
                Log.Debug("忽略过期的 PlaybackStopped");
            }
        };

        lock (_gate)
        {
            _current = playback;
            _output = output;
        }

        output.Init(provider);
        output.Play();

        // 兜底：某些设备/NAudio 版本下 PlaybackStopped 不会按时触发，
        // 只靠它会让朗读卡在第一段。
        _ = Task.Delay(TimeSpan.FromSeconds(seconds + 1.0)).ContinueWith(_ =>
        {
            if (IsCurrent(playback))
            {
                Log.Debug("片段播放结束（时长兜底路径，{Seconds:F1}s）", seconds);
                Complete(playback);
            }
        }, TaskScheduler.Default);

        Log.Debug("开始播放 {Pcm} 字节 PCM（{Format}），时长约 {Seconds:F1}s",
            samples.Length, format, seconds);
    }

    public void Pause()
    {
        lock (_gate) _output?.Pause();
    }

    public void Resume()
    {
        lock (_gate) _output?.Play();
    }

    /// <summary>停止播放（不会触发 <see cref="Finished"/>）。</summary>
    public void Stop()
    {
        WaveOutEvent? output;
        lock (_gate)
        {
            output = _output;
            _output = null;
            // 让 PlaybackStopped / 兜底定时器都失效
            if (_current != null) _current.Cancelled = true;
            _current = null;
        }
        if (output is null) return;

        try { output.Stop(); }
        catch (Exception ex) { Log.Debug(ex, "停止播放时忽略的异常"); }
        output.Dispose();
    }

    public void Dispose() => Stop();

    // ── 内部 ──

    private static (byte[] Samples, WaveFormat? Format) Decode(byte[] mp3)
    {
        using var pcm = new MemoryStream();
        WaveFormat? format = null;

        using var ms = new MemoryStream(mp3);
        IMp3FrameDecompressor? decompressor = null;
        var buffer = new byte[16 * 1024];
        try
        {
            while (true)
            {
                Mp3Frame? frame;
                try
                {
                    frame = Mp3Frame.LoadFromStream(ms);
                }
                catch (EndOfStreamException)
                {
                    break;   // 截断的尾帧：当作结束
                }
                if (frame is null) break;

                decompressor ??= new AcmMp3FrameDecompressor(new Mp3WaveFormat(
                    frame.SampleRate,
                    frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                    frame.FrameLength,
                    frame.BitRate));
                format ??= decompressor.OutputFormat;

                var written = decompressor.DecompressFrame(frame, buffer, 0);
                if (written > 0) pcm.Write(buffer, 0, written);
            }
        }
        finally
        {
            decompressor?.Dispose();
        }

        return (pcm.ToArray(), format);
    }

    private bool IsCurrent(Playback playback)
    {
        lock (_gate) return ReferenceEquals(_current, playback) && !playback.Cancelled;
    }

    private void Complete(Playback playback)
    {
        if (playback.MarkDone()) Finished?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Playback
    {
        private int _done;
        internal volatile bool Cancelled;
        internal bool MarkDone() => Interlocked.Exchange(ref _done, 1) == 0;
    }
}
