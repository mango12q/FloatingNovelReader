using System;
using System.IO;
using NAudio.Wave;

namespace FloatingNovelReader.Services;

/// <summary>
/// mp3 片段的**音频时长**（不解码 PCM，只累加帧的时长）。
///
/// 用途：状态栏「剩余 12:34」与「朗读 N 分钟」的预算必须按**实际音频长度**推进。
/// 用墙钟计时会把合成耗时、缓冲等待都算进 N 分钟里——网慢的时候用户会觉得
/// "明明设了 30 分钟却只听了 20 分钟"，而且倒计时会在两段之间跳。
///
/// ⚠️ 每帧的样本数**不能写死 1152**。edge-tts 返回的是 **MPEG-2 Layer III @ 24kHz**，
/// 每帧只有 **576** 个样本（1152 是 MPEG-1 的值）。写死 1152 会把时长算成整整两倍：
/// 实测一个真实片段被报成 85.1s，而播放器（NAudio 自己按 MPEG 版本解析）只播 42.6s。
/// 这里直接用 NAudio 的 <see cref="Mp3Frame.SampleCount"/>，与播放链路同源，不会算错。
///
/// 纯函数（只读入参字节），可单测。
/// </summary>
public static class TtsSegmentDuration
{
    /// <summary>片段时长；无法解析时返回 <see cref="TimeSpan.Zero"/>。</summary>
    public static TimeSpan OfMp3(byte[]? mp3)
    {
        if (mp3 is null || mp3.Length == 0) return TimeSpan.Zero;

        double seconds = 0;
        try
        {
            using var ms = new MemoryStream(mp3);
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
                if (frame.SampleRate <= 0 || frame.SampleCount <= 0) continue;

                seconds += (double)frame.SampleCount / frame.SampleRate;
            }
        }
        catch (Exception)
        {
            // 帧头损坏等：已经累计到的部分仍然可用，比整段当作 0 合理
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
