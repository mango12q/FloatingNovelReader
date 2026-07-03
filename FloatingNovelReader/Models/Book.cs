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
