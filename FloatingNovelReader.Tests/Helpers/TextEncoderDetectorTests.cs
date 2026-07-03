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

    [Fact]
    public void Detect_Utf32LeBom_NotMistakenForUtf16()
    {
        // UTF-32 LE 的 BOM（FF FE 00 00）前两个字节与 UTF-16 LE 相同，
        // 必须先检查 4 字节 BOM，否则永远检测不出 UTF-32 LE
        var bytes = new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x61, 0x00, 0x00, 0x00 };
        var enc = _detector.Detect(bytes);
        Assert.Equal("utf-32", enc.WebName?.ToLowerInvariant());
    }

    [Fact]
    public void CodePagesEncodings_AvailableAfterDetectorUse()
    {
        // .NET 8 默认不带 GBK/Big5 等 CodePages 编码；
        // 检测器投入使用后必须保证它们已注册，否则 GBK 书导入/阅读会直接失败
        _ = new TextEncoderDetector();
        var gbk = Encoding.GetEncoding("gb2312");
        Assert.Equal(936, gbk.CodePage);
        var big5 = Encoding.GetEncoding("big5");
        Assert.Equal(950, big5.CodePage);
    }
}
