using System;
using System.Linq;
using System.Text;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 朗读文本的清洗 / 转义 / 切分。
/// 对应 edge-tts-plan.md §4.6：原稿把上限写成"200 字"，实际是 **4096 UTF-8 字节**。
/// </summary>
public class TtsSegmenterTests
{
    private const int MaxBytes = TtsProtocol.MaxRequestBytes;   // 4096

    // ── 清洗 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Sanitize_ReplacesControlCharactersThatTheServiceRejects()
    {
        // 竖排制表符 0x0B 在 OCR 出来的 txt 里很常见，不清洗服务端会报错
        var dirty = "前\u000B中\u0000后\u001F尾";

        var clean = TtsSegmenter.Sanitize(dirty);

        Assert.Equal("前 中 后 尾", clean);
        Assert.DoesNotContain('\u000B', clean);
        Assert.DoesNotContain('\u0000', clean);
        Assert.DoesNotContain('\u001F', clean);
    }

    [Fact]
    public void Sanitize_KeepsNormalWhitespaceAndChinese()
    {
        const string text = "第一段。\n第二段\t带制表符，还有空格。";

        Assert.Equal(text, TtsSegmenter.Sanitize(text));
    }

    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TtsSegmenter.Sanitize(null));
        Assert.Equal(string.Empty, TtsSegmenter.Sanitize(""));
    }

    // ── 转义 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EscapeXml_EscapesTheThreeCriticalCharacters()
    {
        Assert.Equal("a&amp;b&lt;c&gt;d", TtsSegmenter.EscapeXml("a&b<c>d"));
    }

    [Fact]
    public void Prepare_SanitizesBeforeEscaping()
    {
        // 顺序很重要：先清洗掉控制字符，再转义；反过来会得到 &amp; 之外的东西
        var prepared = TtsSegmenter.Prepare("A\u000BB&C");

        Assert.Equal("A B&amp;C", prepared);
    }

    [Fact]
    public void Prepare_IsIdempotentOnlyOnce_NotDoubleEscaping()
    {
        // 明确记录：Prepare 只能调用一次；重复调用会二次转义（TSsmlFrame 因此不再转义）
        Assert.Equal("&amp;amp;", TtsSegmenter.Prepare(TtsSegmenter.Prepare("&")));
    }

    // ── 切分 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SplitChapter_UsesPreferredSegmentSizeFarBelowProtocolLimit()
    {
        // 4096 字节的中文合成出来是 4~5 分钟音频 / 1.7MB mp3 / 13MB PCM：
        // 首播延迟和播放缓冲都不可接受，所以实际切片必须小得多。
        Assert.True(TtsSegmenter.PreferredSegmentBytes < TtsProtocol.MaxRequestBytes / 4,
            "切片目标应远小于协议上限");

        var chapter = string.Concat(Enumerable.Repeat("这是一段用于验证切片大小的中文文本。", 200));

        foreach (var s in TtsSegmenter.SplitChapter(chapter))
        {
            Assert.True(Encoding.UTF8.GetByteCount(s) <= TtsSegmenter.PreferredSegmentBytes);
        }
    }

    [Fact]
    public void SplitChapter_ShortText_YieldsSingleSegment()
    {
        var segments = TtsSegmenter.SplitChapter("很短的一章。只有一句话。");

        Assert.Single(segments);
    }

    [Fact]
    public void SplitChapter_LongChapter_EverySegmentWithinByteLimit()
    {
        var chapter = string.Concat(Enumerable.Repeat(
            "这是用于验证切分的一段中文文本，目的是让单次请求不超过服务端允许的字节上限。", 400));

        var segments = TtsSegmenter.SplitChapter(chapter);

        Assert.True(segments.Count > 1, $"应当切成多段，实际 {segments.Count}");
        foreach (var s in segments)
        {
            var bytes = Encoding.UTF8.GetByteCount(s);
            Assert.True(bytes <= MaxBytes, $"某段 {bytes} 字节，超过上限 {MaxBytes}");
        }
    }

    [Fact]
    public void SplitByBytes_PrefersSentenceBoundaries()
    {
        var text = string.Concat(Enumerable.Repeat("这是一句话。", 200));

        var segments = TtsSegmenter.SplitByBytes(text, 100);

        // 绝大多数段应以句号结尾（优先在句末标点断开）
        var endingWithPeriod = segments.Count(s => s.EndsWith('。'));
        Assert.True(endingWithPeriod >= segments.Count - 1,
            $"只有 {endingWithPeriod}/{segments.Count} 段在句末断开");
    }

    [Fact]
    public void SplitByBytes_NeverBreaksSurrogatePairs()
    {
        // emoji 是代理对：在中间切断会产生非法 UTF-16/UTF-8
        var text = string.Concat(Enumerable.Repeat("😀中文", 300));

        var segments = TtsSegmenter.SplitByBytes(text, 64);

        foreach (var s in segments)
        {
            // 每一段都必须能无损往返 UTF-8
            var bytes = Encoding.UTF8.GetBytes(s);
            Assert.Equal(s, Encoding.UTF8.GetString(bytes));

            // 且不能出现**孤立**的代理项（合法的 emoji 代理对是允许的）
            for (var i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]))
                {
                    Assert.True(i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]),
                        "段尾出现孤立的高代理项（代理对被切断了）");
                    i++;
                }
                else
                {
                    Assert.False(char.IsLowSurrogate(s[i]), "段首出现孤立的低代理项");
                }
            }
        }
    }

    [Fact]
    public void SplitByBytes_NeverBreaksXmlEntities()
    {
        var text = string.Concat(Enumerable.Repeat("A&amp;B&lt;C", 300));

        var segments = TtsSegmenter.SplitByBytes(text, 40);

        foreach (var s in segments)
        {
            // 段内每个 '&' 都必须有配对的 ';'，否则实体被切断了
            var i = s.IndexOf('&');
            while (i >= 0)
            {
                Assert.Contains(';', s[i..]);
                i = s.IndexOf('&', i + 1);
            }
            Assert.False(s.EndsWith('&'), "段尾不应是未闭合的 &");
        }
    }

    [Fact]
    public void SplitByBytes_PreservesAllNonWhitespaceContent()
    {
        var text = string.Concat(Enumerable.Repeat("中文内容。", 500));

        var segments = TtsSegmenter.SplitByBytes(text, 128);

        var rejoined = string.Concat(segments);
        Assert.Equal(Strip(text), Strip(rejoined));

        static string Strip(string s) => new(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    [Fact]
    public void SplitByBytes_SingleCharacterOverLimit_DoesNotHang()
    {
        // 上限比一个字符还小：必须能终止并给出结果，不能死循环
        var segments = TtsSegmenter.SplitByBytes("中文内容测试", 2);

        Assert.NotEmpty(segments);
    }

    [Fact]
    public void SplitByBytes_EmptyOrNull_ReturnsEmptyList()
    {
        Assert.Empty(TtsSegmenter.SplitByBytes(null, 4096));
        Assert.Empty(TtsSegmenter.SplitByBytes("", 4096));
        Assert.Empty(TtsSegmenter.SplitByBytes("   ", 4096));
    }
}
