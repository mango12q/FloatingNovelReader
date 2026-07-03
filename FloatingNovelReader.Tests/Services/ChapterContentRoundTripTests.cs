using System;
using System.IO;
using System.Linq;
using System.Text;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 章节字节偏移「往返」测试：
/// 用指定编码写 TXT → 走导入管线（检测/解码/解析）→ 按 Chapter 的字节偏移
/// 从原文件读回内容 → 必须与原文精确一致。
/// 阅读窗口就是按 StartPosition/EndPosition 去文件里 seek 读的，
/// 偏移一错，非 UTF-8 书籍的章节内容就是乱码。
/// </summary>
public class ChapterContentRoundTripTests
{
    private const string Intro = "测试之书\n作者：某人\n\n";
    private const string Ch1 = "第一章 开端\n这是第一章的内容，包含中文标点——以及省略号……\n\n";
    private const string Ch2 = "第二章 发展\n内容稍长一些，再来一行。\n结束。\n";

    private static void AssertRoundTrip(Encoding writeEncoding, string newline = "\n")
    {
        var parts = new[] { Intro, Ch1, Ch2 }
            .Select(s => s.Replace("\n", newline))
            .ToArray();
        var text = string.Concat(parts);
        var path = Path.Combine(Path.GetTempPath(), $"fnr_roundtrip_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, text, writeEncoding);

            // 与 BookImportService.Import 相同的管线（编码已知，聚焦偏移正确性）
            var detector = new TextEncoderDetector();
            var decoded = detector.DecodeFile(path, writeEncoding);
            var fileSize = new FileInfo(path).Length;
            var bomLength = detector.GetPreambleLength(path, writeEncoding);

            var parser = new ChapterParser();
            var book = parser.Parse(decoded, path, fileSize, writeEncoding, bomLength);
            var flat = book.FlatChapters().ToList();
            Assert.Equal(3, flat.Count); // 引言 + 两章

            // 与 ReaderViewModel 相同的读回方式
            for (int i = 0; i < flat.Count; i++)
            {
                var got = ChapterContentReader.Read(path, flat[i], writeEncoding.WebName);
                Assert.Equal(parts[i], got);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RoundTrip_Utf8_NoBom()
        => AssertRoundTrip(new UTF8Encoding(false));

    [Fact]
    public void RoundTrip_Utf8_WithBom()
        => AssertRoundTrip(new UTF8Encoding(true));

    [Fact]
    public void RoundTrip_Utf16Le_WithBom()
        => AssertRoundTrip(Encoding.Unicode);

    [Fact]
    public void RoundTrip_Utf16Be_WithBom()
        => AssertRoundTrip(Encoding.BigEndianUnicode);

    [Fact]
    public void RoundTrip_Utf16Le_CrLf()
        => AssertRoundTrip(Encoding.Unicode, "\r\n");

    [Fact]
    public void RoundTrip_Gbk_CrLf()
    {
        EncodingSupport.EnsureRegistered();
        AssertRoundTrip(Encoding.GetEncoding("gb2312"), "\r\n");
    }

    [Fact]
    public void RoundTrip_Utf16Le_DetectedFromFile()
    {
        // 全链路：记事本「Unicode」另存场景——编码由检测器给出而非人工指定
        var text = Intro + Ch1 + Ch2;
        var path = Path.Combine(Path.GetTempPath(), $"fnr_detect_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, text, Encoding.Unicode);

            var detector = new TextEncoderDetector();
            var encoding = detector.DetectFromFile(path);
            Assert.Equal("utf-16", encoding.WebName);

            var decoded = detector.DecodeFile(path, encoding);
            var bomLength = detector.GetPreambleLength(path, encoding);
            var book = new ChapterParser().Parse(decoded, path, new FileInfo(path).Length, encoding, bomLength);
            var flat = book.FlatChapters().ToList();
            Assert.Equal(3, flat.Count);
            Assert.Equal(Ch1, ChapterContentReader.Read(path, flat[1], encoding.WebName));
            Assert.Equal(Ch2, ChapterContentReader.Read(path, flat[2], encoding.WebName));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
