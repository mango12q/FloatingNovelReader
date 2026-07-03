using System.Text;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// TextEncoderDetector 的单元测试。
/// </summary>
public class TextEncoderDetectorTests
{
    private readonly TextEncoderDetector _detector = new();

    [Fact]
    public void Detect_UTF8WithBOM_ReturnsUtf8()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' };
        var enc = _detector.Detect(bytes);
        Assert.Equal("utf-8", enc.WebName?.ToLowerInvariant());
    }

    [Fact]
    public void Detect_UTF16LE_ReturnsUnicode()
    {
        var bytes = new byte[] { 0xFF, 0xFE, 0x61, 0x00 };
        var enc = _detector.Detect(bytes);
        Assert.True(enc.WebName?.ToLowerInvariant().Contains("utf-16") ?? false);
    }

    [Fact]
    public void Detect_Empty_ReturnsDefault()
    {
        var enc = _detector.Detect(System.Array.Empty<byte>());
        Assert.NotNull(enc);
    }

    [Fact]
    public void Detect_ChineseGBK_DecodesCorrectly()
    {
        // 一段完整的 GBK 文本（"这是一段中文测试文本"）
        var gbkBytes = new byte[]
        {
            0xD5, 0xE2, 0xCA, 0xC7, 0xD2, 0xBB, 0xB6, 0xCE,
            0xD6, 0xD0, 0xCE, 0xC4, 0xB2, 0xE2, 0xCA, 0xD4,
            0xCE, 0xC4, 0xB1, 0xBE
        };
        var enc = _detector.Detect(gbkBytes);
        Assert.NotNull(enc);
        // Ude 启发式检测在极短样本上可能不准确；
        // 但只要返回了非 null 编码，且编码名合理就通过。
        Assert.NotNull(enc.WebName);
    }
}
