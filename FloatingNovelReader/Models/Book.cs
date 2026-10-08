using System;
using System.Collections.Generic;

namespace FloatingNovelReader.Models;

/// <summary>
/// 书籍模型。路径是唯一键，卷和章节通过 Volumes / Chapters 集合维护。
/// </summary>
public sealed class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Encoding { get; set; } = "utf-8";

    /// <summary>
    /// 章节字节偏移所指向的文件。TXT 为 null（偏移直接落在 <see cref="FilePath"/> 上）；
    /// EPUB / PDF 导入时会把正文解析成一份 UTF-8 纯文本缓存，这里存缓存文件路径。
    /// </summary>
    public string? ContentPath { get; set; }

    /// <summary>来源格式：txt / epub / pdf（见 <see cref="BookFormatDetector"/>）。</summary>
    public string SourceFormat { get; set; } = "txt";

    /// <summary>阅读时实际读取正文的文件：优先缓存文件，其次源文件。</summary>
    public string ResolveContentPath() =>
        string.IsNullOrWhiteSpace(ContentPath) ? FilePath : ContentPath!;

    /// <summary>来源格式的枚举视图。</summary>
    public BookFormat Format => BookFormatDetector.FromStorageValue(SourceFormat);

    public int TotalChapters { get; set; }
    public int TotalVolumes { get; set; }
    public DateTime ImportTime { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadTime { get; set; }
    public string CoverColor { get; set; } = "#6C8CFF";

    public List<Volume> Volumes { get; set; } = new();

    /// <summary>获取全部章节的扁平序列（按卷、章顺序）</summary>
    public IEnumerable<Chapter> FlatChapters()
    {
        foreach (var v in Volumes)
            foreach (var c in v.Chapters)
                yield return c;
    }
}
