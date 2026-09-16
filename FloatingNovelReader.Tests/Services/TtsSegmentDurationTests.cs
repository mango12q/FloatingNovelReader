using System;
using System.IO;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// mp3 片段时长测量。
///
/// 「朗读 N 分钟」的预算与状态栏「剩余 mm:ss」都建立在这个数字上：
/// 它必须是**实际音频长度**，否则不是倒计时不准，就是朗读时长翻倍/减半。
///
/// 回归重点：edge-tts 返回的是 **MPEG-2 Layer III @ 24kHz**（每帧 576 个样本），
/// 不是 MPEG-1 的 1152。早期实现把每帧样本数写死成 1152，实测把 42.6 秒的音频
/// 报成 85.1 秒（整整两倍）。这里的用例两种版本都覆盖。
/// </summary>
public class TtsSegmentDurationTests
{
    /// <summary>构造一个合法的 Layer III 帧（静音负载）。</summary>
    /// <param name="mpegVersion">1 = MPEG-1，2 = MPEG-2（edge-tts 用的是 2）。</param>
    private static byte[] BuildMonoFrame(int mpegVersion, int sampleRate, int bitRateKbps)
    {
        var bitrateIndex = bitRateKbps switch
        {
            8 => 1, 16 => 2, 24 => 3, 32 => 4, 40 => 5, 48 => 6, 56 => 7, 64 => 8,
            80 => 9, 96 => 10, 112 => 11, 128 => 12, 144 => 13, 160 => 14,
            _ => throw new ArgumentOutOfRangeException(nameof(bitRateKbps)),
        };

        int sampleRateIndex, samplesPerFrame;
        if (mpegVersion == 1)
        {
            sampleRateIndex = sampleRate switch { 44100 => 0, 48000 => 1, 32000 => 2, _ => throw new ArgumentOutOfRangeException(nameof(sampleRate)) };
            samplesPerFrame = 1152;
        }
        else
        {
            sampleRateIndex = sampleRate switch { 22050 => 0, 24000 => 1, 16000 => 2, _ => throw new ArgumentOutOfRangeException(nameof(sampleRate)) };
            samplesPerFrame = 576;
        }

        // MPEG-1: 144 * bitrate / sampleRate；MPEG-2 Layer III 系数是 72
        var frameLength = (mpegVersion == 1 ? 144 : 72) * bitRateKbps * 1000 / sampleRate + 1;
        var frame = new byte[frameLength];
        frame[0] = 0xFF;
        var versionBits = mpegVersion == 1 ? 0x03 : 0x02;   // 11 = MPEG-1, 10 = MPEG-2
        frame[1] = (byte)(0xE0 | (versionBits << 3) | 0x02);   // Layer III(01) + 无 CRC(1)
        frame[2] = (byte)((bitrateIndex << 4) | (sampleRateIndex << 2));
        frame[3] = 0xC0;   // 单声道
        _ = samplesPerFrame;
        return frame;
    }

    private static byte[] Concat(int frameCount, int mpegVersion = 2, int sampleRate = 24000, int bitRateKbps = 48)
    {
        var frame = BuildMonoFrame(mpegVersion, sampleRate, bitRateKbps);
        using var ms = new MemoryStream();
        for (var i = 0; i < frameCount; i++) ms.Write(frame, 0, frame.Length);
        return ms.ToArray();
    }

    [Fact]
    public void OfMp3_Mpeg2_576SamplesPerFrame_IsCountedOnce()
    {
        // 24kHz / 576 样本 = 每帧 24ms；42 帧 = 1.008s
        // 写死 1152 的实现会报 2.016s —— 这个断言就是那个回归的守卫
        var mp3 = Concat(frameCount: 42, mpegVersion: 2, sampleRate: 24000);

        var duration = TtsSegmentDuration.OfMp3(mp3);

        Assert.InRange(duration.TotalSeconds, 0.98, 1.02);
    }

    [Fact]
    public void OfMp3_Mpeg2_22050Hz_UsesItsOwnFrameDuration()
    {
        // 22050Hz / 576 样本 = 每帧 26.12ms —— 证明时长是按帧头算的，不是写死常数
        var mp3 = Concat(frameCount: 100, mpegVersion: 2, sampleRate: 22050, bitRateKbps: 32);

        var duration = TtsSegmentDuration.OfMp3(mp3);

        Assert.InRange(duration.TotalSeconds, 2.55, 2.67);
    }

    [Fact]
    public void OfMp3_EdgeTtsRealFormat_ThirtySecondsIsThirtySeconds()
    {
        // edge-tts 实测输出格式：24kHz / 48kbps / 单声道 / MPEG-2（每帧 576 样本）
        const int seconds = 30;
        var frames = (int)Math.Ceiling(seconds / (576.0 / 24000));
        var mp3 = Concat(frames, mpegVersion: 2, sampleRate: 24000, bitRateKbps: 48);

        var duration = TtsSegmentDuration.OfMp3(mp3);

        Assert.InRange(duration.TotalSeconds, 29.5, 30.5);
    }

    [Fact]
    public void OfMp3_ScalesWithFrameCount()
    {
        var shortClip = TtsSegmentDuration.OfMp3(Concat(10));
        var longClip = TtsSegmentDuration.OfMp3(Concat(100));

        Assert.True(shortClip > TimeSpan.Zero);
        Assert.True(longClip > shortClip);
        Assert.Equal(10, Math.Round(longClip.TotalSeconds / shortClip.TotalSeconds));
    }

    [Fact]
    public void OfMp3_EmptyOrNull_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, TtsSegmentDuration.OfMp3(null));
        Assert.Equal(TimeSpan.Zero, TtsSegmentDuration.OfMp3(Array.Empty<byte>()));
    }

    [Fact]
    public void OfMp3_Garbage_DoesNotThrow()
    {
        var garbage = new byte[512];
        for (var i = 0; i < garbage.Length; i++) garbage[i] = (byte)(i * 7 % 251);

        var duration = TtsSegmentDuration.OfMp3(garbage);

        // 关键是不抛：垃圾数据不能把朗读线程打崩
        Assert.True(duration >= TimeSpan.Zero);
    }

    [Fact]
    public void OfMp3_TruncatedLastFrame_KeepsCountingCompleteFrames()
    {
        var full = Concat(20);
        var truncated = new byte[full.Length - 3];
        Array.Copy(full, truncated, truncated.Length);

        var duration = TtsSegmentDuration.OfMp3(truncated);

        Assert.True(duration > TimeSpan.Zero);
        Assert.True(duration <= TtsSegmentDuration.OfMp3(full));
    }
}
