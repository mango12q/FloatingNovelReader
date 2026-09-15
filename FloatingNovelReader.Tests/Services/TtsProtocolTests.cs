using System;
using System.Text;
using System.Text.Json;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// edge-tts 协议层（纯函数部分）。
///
/// Sec-MS-GEC 的 4 个期望值来自 Python 参考实现 rany2/edge-tts 的 drm.generate_sec_ms_gec()，
/// 是探针跑出来的真实 oracle —— 算法改动（时间戳取整 / 拼接顺序 / 大小写）会立刻失败。
/// 帧布局的期望值来自探针抓到的真实二进制帧。
/// </summary>
public class TtsProtocolTests
{
    // ── Sec-MS-GEC ────────────────────────────────────────────────────────────

    [Theory]
    // 12:34:56 → 取整到 12:30:00
    [InlineData(2026, 9, 15, 12, 34, 56, "1FC57D28399824DCB8164E42BE923CC3EFAE4A4F06BF80DC8C90B0EE102C1BC9")]
    // 12:36:00 → 取整到 12:35:00（与上一条不同的 5 分钟窗口）
    [InlineData(2026, 9, 15, 12, 36, 0, "EF5E86CA4A0DB3CC28ECD5A35BE8AC6A0C53C14059174313344BD94070D5AEBC")]
    [InlineData(2026, 1, 1, 0, 0, 0, "42D1947403FD94975436C65DBFCA8003073F9CB5C3F1CE25AF8961A11D7C3DFE")]
    [InlineData(2026, 7, 4, 23, 59, 59, "14842020457990B99DCC816E5E022A617295ED62DD635B3E2FF3ED5065D5539E")]
    public void BuildSecMsGec_MatchesPythonReferenceImplementation(
        int y, int mo, int d, int h, int mi, int s, string expected)
    {
        var gec = TtsProtocol.BuildSecMsGec(new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc));

        Assert.Equal(expected, gec);
    }

    [Fact]
    public void BuildSecMsGec_IsUppercase()
    {
        var gec = TtsProtocol.BuildSecMsGec(new DateTime(2026, 9, 15, 12, 34, 56, DateTimeKind.Utc));

        Assert.Equal(gec.ToUpperInvariant(), gec);
    }

    [Fact]
    public void BuildSecMsGec_IsStableWithinTheSameFiveMinuteWindow()
    {
        var a = TtsProtocol.BuildSecMsGec(new DateTime(2026, 9, 15, 12, 30, 1, DateTimeKind.Utc));
        var b = TtsProtocol.BuildSecMsGec(new DateTime(2026, 9, 15, 12, 34, 59, DateTimeKind.Utc));
        var c = TtsProtocol.BuildSecMsGec(new DateTime(2026, 9, 15, 12, 35, 0, DateTimeKind.Utc));

        // 同一窗口内必须相同 —— 这正是"5 分钟有效期"的实现方式（向下取整）
        Assert.Equal(a, b);
        // 跨窗口必须不同
        Assert.NotEqual(b, c);
    }

    // ── 连接参数 ──────────────────────────────────────────────────────────────

    [Fact]
    public void NewConnectionId_IsDashlessUuidHex()
    {
        var id = TtsProtocol.NewConnectionId();

        Assert.Equal(32, id.Length);
        Assert.DoesNotContain("-", id);
    }

    [Fact]
    public void NewMuid_Is32UppercaseHex()
    {
        var muid = TtsProtocol.NewMuid();

        Assert.Equal(32, muid.Length);
        Assert.Equal(muid.ToUpperInvariant(), muid);
    }

    [Fact]
    public void BuildWssUrl_ContainsEveryRequiredQueryParameter()
    {
        var url = TtsProtocol.BuildWssUrl(new DateTime(2026, 9, 15, 12, 34, 56, DateTimeKind.Utc), "abc123");

        Assert.StartsWith("wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1?", url);
        Assert.Contains($"TrustedClientToken={TtsProtocol.TrustedClientToken}", url);
        Assert.Contains("&ConnectionId=abc123", url);
        Assert.Contains("&Sec-MS-GEC=", url);
        Assert.Contains("&Sec-MS-GEC-Version=1-143.0.3650.75", url);
    }

    [Fact]
    public void SecMsGecVersion_MatchesChromiumVersionAndUserAgentMajor()
    {
        Assert.Equal("1-" + TtsProtocol.ChromiumFullVersion, TtsProtocol.SecMsGecVersion);
        Assert.Contains($"Edg/{TtsProtocol.ChromiumMajor}.0.0.0", TtsProtocol.UserAgent);
    }

    // ── speech.config 帧 ──────────────────────────────────────────────────────

    [Fact]
    public void SpeechConfigFrame_IsWellFormedJsonWithBalancedBraces()
    {
        var frame = TtsProtocol.BuildSpeechConfigFrame(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));

        var body = frame[(frame.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..].TrimEnd('\r', '\n');
        using var doc = JsonDocument.Parse(body);   // 花括号少一个这里就会抛

        var audio = doc.RootElement
            .GetProperty("context").GetProperty("synthesis").GetProperty("audio");
        Assert.Equal(TtsProtocol.OutputFormat, audio.GetProperty("outputFormat").GetString());
        // metadataoptions 是必需块
        Assert.True(audio.TryGetProperty("metadataoptions", out _));
    }

    [Fact]
    public void SpeechConfigFrame_HasPathHeaderAndNoRequestId()
    {
        var frame = TtsProtocol.BuildSpeechConfigFrame(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains("Path:speech.config", frame);
        Assert.Contains("Content-Type:application/json; charset=utf-8", frame);
        // 与 SSML 帧不同，这一帧**不带** X-RequestId
        Assert.DoesNotContain("X-RequestId", frame);
    }

    // ── SSML 帧 ───────────────────────────────────────────────────────────────

    [Fact]
    public void SsmlFrame_HasPathSsmlAndTimestampEndingWithZ()
    {
        var frame = TtsProtocol.BuildSsmlFrame(
            "req1", new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc),
            "zh-CN-XiaoxiaoNeural", "你好", 0, 0);

        Assert.Contains("X-RequestId:req1", frame);
        Assert.Contains("Content-Type:application/ssml+xml", frame);
        Assert.Contains("Path:ssml", frame);
        // X-Timestamp 以 Z 结尾（微软的 bug，但必须照发）
        Assert.Matches(@"X-Timestamp:.*Universal Time\)Z", frame);
    }

    [Fact]
    public void SsmlFrame_DoesNotReEscapeAlreadyEscapedText()
    {
        var frame = TtsProtocol.BuildSsmlFrame(
            "req1", new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc),
            "v", "&amp;", 0, 0);

        // 传入已是转义后的文本，输出里应仍是 &amp; 而不是 &amp;amp;
        Assert.Contains(">&amp;<", frame);
        Assert.DoesNotContain("&amp;amp;", frame);
    }

    [Theory]
    [InlineData(0, "+0%")]
    [InlineData(12, "+12%")]
    [InlineData(-50, "-50%")]
    [InlineData(50.4, "+50%")]
    public void SignedPercent_FormatsAsEdgeExpects(double value, string expected)
    {
        Assert.Equal(expected, TtsProtocol.SignedPercent(value));
    }

    // ── 二进制音频帧 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 用探针实测的布局构造帧：[2 字节大端长度 L][L 字节 header][负载]。
    /// 实测 L=128、负载以 FF F3（MP3 同步字）开头。
    /// </summary>
    private static byte[] RealishBinaryFrame()
    {
        var header = "X-RequestId:deadbeef\r\nContent-Type:audio/mpeg\r\n" +
                     "X-StreamId:ABC\r\nPath:audio\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        var payload = new byte[] { 0xFF, 0xF3, 0x64, 0xC4, 0x00, 0x00, 0x00, 0x03 };

        var frame = new byte[2 + headerBytes.Length + payload.Length];
        frame[0] = (byte)(headerBytes.Length >> 8);
        frame[1] = (byte)(headerBytes.Length & 0xFF);
        headerBytes.CopyTo(frame, 2);
        payload.CopyTo(frame, 2 + headerBytes.Length);
        return frame;
    }

    [Fact]
    public void TryParseBinaryAudioFrame_ReadsLengthPrefixedLayout()
    {
        var frame = RealishBinaryFrame();

        Assert.True(TtsProtocol.TryParseBinaryAudioFrame(frame, out var header, out var payload));

        Assert.Contains("Path:audio", header);
        Assert.Equal("audio/mpeg", TtsProtocol.HeaderValue(header, "Content-Type"));
        Assert.Equal("audio", TtsProtocol.HeaderValue(header, "Path"));
        Assert.True(TtsProtocol.LooksLikeMp3(payload));
        Assert.Equal(new byte[] { 0xFF, 0xF3 }, payload[..2]);
    }

    [Fact]
    public void TryParseBinaryAudioFrame_SplittingAtLengthPlusFourWouldBeWrong()
    {
        // 反证：把负载起点当成长度 + 4（另一种猜测）会得到非 MP3 数据
        var frame = RealishBinaryFrame();
        Assert.True(TtsProtocol.TryParseBinaryAudioFrame(frame, out _, out var correct));

        var length = (frame[0] << 8) | frame[1];
        var wrongOffset = 2 + length + 2;
        var wrong = frame[wrongOffset..];

        Assert.True(TtsProtocol.LooksLikeMp3(correct));
        Assert.False(TtsProtocol.LooksLikeMp3(wrong));
    }

    [Theory]
    [InlineData(new byte[0])]                          // 空
    [InlineData(new byte[] { 0x00 })]                  // 只有一个字节
    [InlineData(new byte[] { 0x00, 0x80, 0x01 })]      // 声称 128 字节但只有 1 字节
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x02 })] // 长度 0
    public void TryParseBinaryAudioFrame_RejectsMalformedFrames(byte[] frame)
    {
        Assert.False(TtsProtocol.TryParseBinaryAudioFrame(frame, out _, out _));
    }

    [Fact]
    public void TryParseBinaryAudioFrame_EmptyPayload_IsRejectedButNotThrowing()
    {
        // 服务端在 turn.end 前会发一个空负载的二进制帧，解析方必须能安全跳过
        var header = Encoding.UTF8.GetBytes("Path:audio\r\n");
        var frame = new byte[2 + header.Length];
        frame[0] = 0;
        frame[1] = (byte)header.Length;
        header.CopyTo(frame, 2);

        Assert.False(TtsProtocol.TryParseBinaryAudioFrame(frame, out _, out _));
    }

    [Fact]
    public void HeaderValue_IsCaseSensitiveAndReturnsNullWhenMissing()
    {
        const string header = "Path:audio\r\nContent-Type:audio/mpeg\r\n";

        Assert.Equal("audio", TtsProtocol.HeaderValue(header, "Path"));
        Assert.Null(TtsProtocol.HeaderValue(header, "path"));
        Assert.Null(TtsProtocol.HeaderValue(header, "X-NotThere"));
    }
}
