using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FloatingNovelReader.Helpers;

/// <summary>HTML 里识别到的标题：<paramref name="Index"/> 是它在转换后纯文本中的字符下标（行首）。</summary>
public sealed record HtmlHeading(string Title, int Index);

/// <summary>HTML → 纯文本的结果。</summary>
public sealed class HtmlTextResult
{
    public HtmlTextResult(string text, IReadOnlyList<HtmlHeading> headings)
    {
        Text = text;
        Headings = headings;
    }

    public string Text { get; }

    public IReadOnlyList<HtmlHeading> Headings { get; }
}

/// <summary>
/// EPUB 内 XHTML → 纯文本。不引入 HTML 解析库：EPUB 正文是「近似良构」的 XML/HTML，
/// 用标签级正则处理既够用又能容忍残缺标签（XDocument 遇到一个未闭合标签就整章失败）。
///
/// 关键约定：标题（h1~h6）会被单独提成一行，并记录它在结果文本中的下标，
/// 这样「一个 XHTML 里塞了整本书」的 EPUB 也能按标题切成多章。
/// </summary>
public static class HtmlTextConverter
{
    private static readonly Regex ReComment = new(
        @"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ReScriptStyle = new(
        @"<(script|style)\b[^>]*>.*?</\1\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReHead = new(
        @"<head\b[^>]*>.*?</head\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 块级标签 → 换行（不含 h1~h6：标题由标题通道单独处理，且要记录下标）
    private static readonly Regex ReBlockTag = new(
        @"</?(?:p|div|br|li|ul|ol|tr|td|th|table|tbody|thead|blockquote|section|article|aside|header|footer|figure|figcaption|pre|hr|nav|dl|dt|dd)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReHeading = new(
        @"<h([1-6])\b[^>]*>(.*?)</h\1\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReAnyTag = new(@"<[^>]*>", RegexOptions.Compiled);

    private static readonly Regex ReTitle = new(
        @"<title\b[^>]*>(.*?)</title\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 行内空白：普通空格 + 制表 + NBSP 等
    private static readonly Regex ReInlineSpace = new(@"[ \t\f\v\u00A0\u2000-\u200B]+", RegexOptions.Compiled);

    /// <summary>把一段 XHTML 转成纯文本，同时返回标题及其下标。</summary>
    public static HtmlTextResult ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return new HtmlTextResult(string.Empty, Array.Empty<HtmlHeading>());

        var work = ReComment.Replace(html, " ");
        work = ReScriptStyle.Replace(work, " ");
        work = ReHead.Replace(work, " ");
        work = ReBlockTag.Replace(work, "\n");

        var sb = new StringBuilder(work.Length);
        var headings = new List<HtmlHeading>();
        int cursor = 0;

        foreach (Match m in ReHeading.Matches(work))
        {
            if (m.Index > cursor)
                AppendFragment(sb, work.Substring(cursor, m.Index - cursor));

            var title = CleanInline(m.Groups[2].Value);
            if (title.Length > 0)
            {
                EnsureLineBreak(sb);
                headings.Add(new HtmlHeading(title, sb.Length));
                sb.Append(title).Append('\n');
            }
            cursor = m.Index + m.Length;
        }

        if (cursor < work.Length)
            AppendFragment(sb, work.Substring(cursor));

        // 只裁掉末尾换行：开头若被裁，记录下来的标题下标就会整体错位
        return new HtmlTextResult(sb.ToString().TrimEnd('\n'), headings);
    }

    /// <summary>取第一个 h1~h6 的文本；没有则取 &lt;title&gt;；都没有返回 null。</summary>
    public static string? ExtractHeadingTitle(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;

        var m = ReHeading.Match(html);
        if (m.Success)
        {
            var t = CleanInline(m.Groups[2].Value);
            if (t.Length > 0) return t;
        }

        var tm = ReTitle.Match(html);
        if (tm.Success)
        {
            var t = CleanInline(tm.Groups[1].Value);
            if (t.Length > 0) return t;
        }

        return null;
    }

    /// <summary>
    /// 去掉标签、解码实体、把空白（含换行）压成单个空格（用于单行文本，如章节标题）。
    /// 标题里带换行很常见（EPUB 的 &lt;h1&gt; 常写成两行），带进目录会显示成断行。
    /// </summary>
    public static string CleanInline(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var noTags = ReAnyTag.Replace(raw, " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    /// <summary>追加一段正文：剥标签 → 解实体 → 逐行压空白 → 最多保留一个空行。</summary>
    private static void AppendFragment(StringBuilder sb, string raw)
    {
        var text = NormalizeLines(StripAndDecode(raw));
        if (text.Length == 0) return;

        EnsureLineBreak(sb);
        sb.Append(text);
        if (sb.Length > 0 && sb[^1] != '\n') sb.Append('\n');
    }

    private static string StripAndDecode(string raw)
    {
        if (raw.Length == 0) return string.Empty;
        var noTags = ReAnyTag.Replace(raw, " ");
        return WebUtility.HtmlDecode(noTags);
    }

    /// <summary>逐行去首尾空白、合并行内空白，并把连续空行压成一个。</summary>
    internal static string NormalizeLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        int blankRun = 0;
        int start = 0;

        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != '\n' && text[i] != '\r') continue;

            var line = ReInlineSpace.Replace(text.Substring(start, i - start), " ").Trim();
            if (line.Length == 0)
            {
                blankRun++;
            }
            else
            {
                if (blankRun > 0 && sb.Length > 0) sb.Append('\n');
                blankRun = 0;
                sb.Append(line).Append('\n');
            }

            // \r\n 视为一个换行
            if (i < text.Length && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            start = i + 1;
        }

        return sb.ToString().Trim('\n');
    }

    private static void EnsureLineBreak(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[^1] != '\n') sb.Append('\n');
    }
}
