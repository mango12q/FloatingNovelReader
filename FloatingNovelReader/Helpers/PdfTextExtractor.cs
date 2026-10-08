using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FloatingNovelReader.Models;
using Serilog;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// PDF → <see cref="ExtractedContent"/>（只抽文字层，不做 OCR）。
///
/// 章节切分策略（依次降级）：
///   1. 在每页文字里按「第 N 章 / Chapter N / 卷一」等标题行切（复用 ChapterParser.LooksLikeHeader）
///   2. 全篇都没有标题行时，按固定页数分块（第 1-5 页 / 第 6-10 页 …）
/// 抽不到文字（扫描版图片 PDF）直接报错，提示用户换有文字层的版本。
/// </summary>
public static class PdfTextExtractor
{
    /// <summary>没有标题行时每个章节段的页数。</summary>
    internal const int PagesPerSectionFallback = 5;

    /// <summary>整篇文字少于这个长度就认为「没有文字层」。</summary>
    internal const int MinUsefulTextLength = 20;

    /// <summary>
    /// 页数上限。超大 PDF 每页都要解析内容流，没有上限时导入会像卡死一样久，
    /// 而用户完全不知道还要等多久。
    /// </summary>
    internal const int MaxPages = 20000;

    private static readonly Regex ReCjkSpace = new(
        @"(?<=[\u3000-\u303F\u4E00-\u9FFF\uFF00-\uFFEF])[ \t]+(?=[\u3000-\u303F\u4E00-\u9FFF\uFF00-\uFFEF])",
        RegexOptions.Compiled);

    public static ExtractedContent Extract(string filePath)
    {
        PdfDocument document;
        try
        {
            document = PdfDocument.Open(filePath, new ParsingOptions { UseLenientParsing = true });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "这个 PDF 打不开：可能已损坏、受密码保护，或者不是标准 PDF 文件。", ex);
        }

        using (document)
        {
            var content = new ExtractedContent
            {
                Title = NullIfBlank(document.Information?.Title) ?? string.Empty,
                Author = NullIfBlank(document.Information?.Author),
            };

            var pages = new List<string>(Math.Max(4, document.NumberOfPages));
            if (document.NumberOfPages > MaxPages)
            {
                throw new InvalidOperationException(
                    $"这个 PDF 有 {document.NumberOfPages} 页，超过 {MaxPages} 页的解析上限。");
            }

            // 逐页取文字：单页坏掉（内容流损坏）只跳过这一页，不让整本书导入失败
            for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
            {
                try
                {
                    pages.Add(ExtractPageText(document.GetPage(pageNumber)));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "PDF 第 {Page} 页解析失败，已跳过", pageNumber);
                    pages.Add(string.Empty);
                }
            }

            var totalChars = pages.Sum(p => p.Length);
            if (totalChars < MinUsefulTextLength)
            {
                throw new InvalidOperationException(
                    "这个 PDF 里没有可提取的文字（多半是扫描版图片 PDF）。" +
                    "请换带文字层的版本，或先用 OCR 工具转换后再导入。");
            }

            BuildSections(content, pages);

            if (content.Sections.Count == 0)
                throw new InvalidOperationException("这个 PDF 里没有解析出正文。");

            Log.Information("PDF 解析完成 {File}: 页数={Pages} 章节={Count} 字符={Chars}",
                Path.GetFileName(filePath), pages.Count, content.Sections.Count, content.TotalChars);
            return content;
        }
    }

    /// <summary>按标题行切章；没有标题行则按页分块。</summary>
    internal static void BuildSections(ExtractedContent content, IReadOnlyList<string> pages)
    {
        var currentTitle = (string?)null;
        var buffer = new StringBuilder();
        int sectionStartPage = 1;

        void Flush(int endPage)
        {
            var body = Reflow(buffer.ToString()).Trim();
            if (body.Length == 0) return;
            var title = currentTitle;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = sectionStartPage == endPage
                    ? $"第 {sectionStartPage} 页"
                    : $"第 {sectionStartPage}-{endPage} 页";
            }
            content.Sections.Add(new ExtractedSection(title!, body));
        }

        bool sawHeader = false;
        for (int i = 0; i < pages.Count; i++)
        {
            int pageNumber = i + 1;
            var pageText = pages[i];
            if (pageText.Length == 0) continue;

            foreach (var rawLine in pageText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                if (ChapterParser.LooksLikeHeader(line))
                {
                    Flush(pageNumber);
                    buffer.Clear();
                    currentTitle = line;
                    sectionStartPage = pageNumber;
                    sawHeader = true;
                    continue;
                }

                if (buffer.Length > 0) buffer.Append('\n');
                buffer.Append(line);
            }
        }
        Flush(pages.Count);

        if (sawHeader) return;

        // 全篇没有标题行：按固定页数分块，至少让目录可用
        content.Sections.Clear();
        for (int start = 0; start < pages.Count; start += PagesPerSectionFallback)
        {
            int end = Math.Min(start + PagesPerSectionFallback, pages.Count);
            var body = Reflow(string.Join("\n", pages.Skip(start).Take(end - start))).Trim();
            if (body.Length == 0) continue;

            var title = end - start == 1 ? $"第 {start + 1} 页" : $"第 {start + 1}-{end} 页";
            content.Sections.Add(new ExtractedSection(title, body));
        }
    }

    /// <summary>取一页的文字（优先保持阅读顺序，失败退回 page.Text）。</summary>
    private static string ExtractPageText(UglyToad.PdfPig.Content.Page page)
    {
        string text;
        try
        {
            text = ContentOrderTextExtractor.GetText(page, true);
        }
        catch
        {
            text = page.Text ?? string.Empty;
        }

        if (string.IsNullOrEmpty(text)) text = page.Text ?? string.Empty;
        return NormalizeCjkSpacing(text);
    }

    /// <summary>去掉 CJK 字符之间被误插的单个空格（PDF 字距导致的常见噪声）。</summary>
    internal static string NormalizeCjkSpacing(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : ReCjkSpace.Replace(text, string.Empty);

    /// <summary>
    /// PDF 的换行是「排版换行」而不是段落边界：把没有以句末标点结束的行接回上一行，
    /// 否则每一行都会在阅读窗口里被当成一个独立段落，中文读起来支离破碎。
    /// </summary>
    internal static string Reflow(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder(text.Length);
        var pending = new StringBuilder();
        int pendingLines = 0;

        void Commit()
        {
            if (pending.Length == 0) return;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(pending);
            pending.Clear();
            pendingLines = 0;
        }

        foreach (var raw in lines)
        {
            var line = HtmlTextConverter.NormalizeLines(raw);
            if (line.Length == 0)
            {
                // 空行 = 段落分隔，先把攒着的行落定
                Commit();
                continue;
            }

            if (pending.Length == 0)
            {
                pending.Append(line);
                pendingLines = 1;
                continue;
            }

            if (ShouldJoin(pending, line, pendingLines == 1))
            {
                if (NeedsSpace(pending[^1], line[0])) pending.Append(' ');
                pending.Append(line);
                pendingLines++;
            }
            else
            {
                Commit();
                pending.Append(line);
                pendingLines = 1;
            }
        }
        Commit();

        return sb.ToString();
    }

    /// <summary>
    /// 上一行没写完（不是句末标点、且够长）且本行不像新段落开头时，接行。
    /// 独立成行的标题既不吸收下一行、也不会被上一行吸收。
    /// </summary>
    private static bool ShouldJoin(StringBuilder previous, string next, bool previousIsSingleLine)
    {
        if (previous.Length < 12) return false;

        var last = previous[^1];
        if (IsSentenceEnd(last)) return false;

        if (previousIsSingleLine && ChapterParser.LooksLikeHeader(previous.ToString())) return false;

        // 本行像新的段落开头：标题、编号列表、项目符号
        if (ChapterParser.LooksLikeHeader(next)) return false;
        if (next.Length > 0 && (next[0] == '•' || next[0] == '·' || next[0] == '-' || next[0] == '＊' || next[0] == '*'))
            return false;
        if (next.Length > 1 && char.IsDigit(next[0]) && (next[1] == '｜' || next[1] == '|' || next[1] == '、' || next[1] == '.'))
            return false;

        return true;
    }

    private static bool IsSentenceEnd(char c) =>
        c is '。' or '！' or '？' or '…' or '；' or '：' or '”' or '’' or '"' or '\'' or ')' or '）' or '》' or '」' or '』'
          or '.' or '!' or '?' or ';' or ':' or ']' or '】';

    private static bool NeedsSpace(char left, char right) =>
        IsAsciiWordChar(left) && IsAsciiWordChar(right);

    private static bool IsAsciiWordChar(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
