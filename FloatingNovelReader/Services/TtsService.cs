using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

public sealed class TtsProgressEventArgs : EventArgs
{
    public TtsProgressEventArgs(int segmentIndex, int segmentCount, string text, bool drivesReader)
    {
        SegmentIndex = segmentIndex;
        SegmentCount = segmentCount;
        Text = text;
        DrivesReader = drivesReader;
    }

    /// <summary>0 基的段序号。</summary>
    public int SegmentIndex { get; }
    public int SegmentCount { get; }
    public string Text { get; }

    /// <summary>
    /// 这次朗读是否用于驱动阅读窗口（true）还是仅试听（false）。
    /// 试听不该改动阅读位置和状态栏。
    /// </summary>
    public bool DrivesReader { get; }
}

/// <summary>
/// 朗读服务：把一章文本切片 → 逐段合成 → 顺序播放。
///
/// 与 AutoReadService 一样是"驱动阅读的独立服务"，两者互不认识：
/// 谁被启动由 ReaderViewModel 决定（软互斥由 VM 负责），这里只做合成与播放。
///
/// 本期（第一刀）范围：**朗读当前章节**，段级进度与停止。
/// 未实现（后续刀）：磁盘缓存、后台预合成、N 分钟 / N 章停止条件、Azure 后端。
/// </summary>
public sealed class TtsService : IDisposable
{
    private readonly TtsEdgeClient _client;
    private readonly TtsPlayer _player;
    private readonly SettingsService _settings;

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private volatile bool _isRunning;

    public TtsService(TtsEdgeClient client, TtsPlayer player, SettingsService settings)
    {
        _client = client;
        _player = player;
        _settings = settings;
    }

    public bool IsRunning => _isRunning;

    public event EventHandler? Started;
    public event EventHandler? Stopped;
    public event EventHandler<TtsProgressEventArgs>? Progress;
    public event EventHandler? SegmentFinished;
    public event EventHandler<string>? Failed;

    /// <summary>开始朗读一段文本（会先停掉正在进行的朗读）。</summary>
    /// <param name="drivesReader">
    /// true = 作为阅读器朗读（进度会驱动状态栏与阅读位置）；
    /// false = 仅试听（不该影响阅读位置）。
    /// </param>
    public void Speak(string chapterText, bool drivesReader = true)
    {
        Stop();

        var segments = TtsSegmenter.SplitChapter(chapterText);
        if (segments.Count == 0)
        {
            Log.Information("朗读：没有可合成的文本");
            return;
        }

        var cts = new CancellationTokenSource();
        _cts = cts;
        Log.Information("朗读开始：共 {Count} 段（drivesReader={Drives}）", segments.Count, drivesReader);
        _worker = Task.Run(() => RunAsync(segments, drivesReader, cts.Token), CancellationToken.None);
    }

    public void Stop()
    {
        var cts = _cts;
        _cts = null;
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        _player.Stop();
    }

    private async Task RunAsync(IReadOnlyList<string> segments, bool drivesReader, CancellationToken ct)
    {
        _isRunning = true;
        Started?.Invoke(this, EventArgs.Empty);

        try
        {
            for (var i = 0; i < segments.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                Progress?.Invoke(this, new TtsProgressEventArgs(i, segments.Count, segments[i], drivesReader));

                var mp3 = await _client.SynthesizeAsync(_settings.Current.Tts, segments[i], ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                await PlayAndWaitAsync(mp3, ct).ConfigureAwait(false);
                SegmentFinished?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("朗读已取消");
        }
        catch (Exception ex)
        {
            // 网络断开 / 静默截断 / 服务端拒绝都走这里：如实上报，不静默吞掉
            Log.Error(ex, "朗读失败");
            Failed?.Invoke(this, ex.Message);
        }
        finally
        {
            _player.Stop();
            _isRunning = false;
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task PlayAndWaitAsync(byte[] mp3, CancellationToken ct)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFinished(object? sender, EventArgs e) => finished.TrySetResult();

        _player.Finished += OnFinished;
        try
        {
            var volume = 1.0 + _settings.Current.Tts.VolumePercent / 100.0;
            _player.PlaySegment(mp3, volume);
            await finished.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _player.Finished -= OnFinished;
        }
    }

    public void Dispose()
    {
        Stop();
        _player.Dispose();
    }
}
