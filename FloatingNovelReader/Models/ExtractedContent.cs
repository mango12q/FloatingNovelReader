using System.Collections.Generic;

namespace FloatingNovelReader.Models;

/// <summary>
/// EPUB / PDF 解析出的中间结果：一本书 = 若干「章节段」。
/// 解析器只负责把电子书拆成标题 + 正文，落盘成纯文本与字节偏移由 ExtractedTextCache 负责。
/// </summary>
public sealed class ExtractedContent
{
    /// <summary>书名（来自 EPUB 元数据 / PDF 文档信息，缺失时由调用方回退成文件名）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>作者（可空）。</summary>
    public string? Author { get; set; }

    /// <summary>章节段列表，顺序即阅读顺序。</summary>
    public List<ExtractedSection> Sections { get; } = new();

    /// <summary>正文总字符数（用于日志与「解析出空内容」的判断）。</summary>
    public long TotalChars
    {
        get
        {
            long total = 0;
            foreach (var s in Sections) total += s.Text.Length;
            return total;
        }
    }
}

/// <summary>一个章节段：标题 + 正文（正文不含标题行）。</summary>
public sealed class ExtractedSection
{
    public ExtractedSection(string title, string text)
    {
        Title = title;
        Text = text;
    }

    public string Title { get; }

    public string Text { get; }
}
