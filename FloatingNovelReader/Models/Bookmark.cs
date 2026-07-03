using System;

namespace FloatingNovelReader.Models;

/// <summary>
/// 书签。指向某本书的某章的某页。
/// </summary>
public sealed class Bookmark
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int ChapterId { get; set; }
    public int PageNumber { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
