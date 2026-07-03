using FloatingNovelReader.Models;

namespace FloatingNovelReader.ApplicationServices;

/// <summary>
/// 书籍应用服务接口。定义书架场景的所有用例。
/// </summary>
public interface IBookService
{
    /// <summary>获取书架列表（含搜索和排序）</summary>
    Task<IReadOnlyList<Book>> GetBookshelfAsync(string? search, string orderBy, CancellationToken ct = default);

    /// <summary>根据 Id 获取书籍（不含 Volumes/Chapters）</summary>
    Task<Book?> GetBookAsync(int bookId, CancellationToken ct = default);

    /// <summary>获取书籍完整结构（含 Volumes + Chapters，一次性查全）</summary>
    Task<BookDetail?> GetBookDetailAsync(int bookId, CancellationToken ct = default);

    /// <summary>导入 TXT 文件</summary>
    Task<BookImportResult> ImportAsync(string filePath, CancellationToken ct = default);

    /// <summary>从书架移除书籍</summary>
    /// <param name="deleteSourceFile">是否同时删除源 .txt 文件</param>
    Task RemoveAsync(int bookId, bool deleteSourceFile, CancellationToken ct = default);

    /// <summary>刷新书架缓存</summary>
    Task ReloadAsync(string? search = null, string orderBy = "LastReadTime", CancellationToken ct = default);
}

/// <summary>书籍详情（完整结构，供 ReaderViewModel 使用）</summary>
public sealed class BookDetail
{
    public Book Book { get; }
    public IReadOnlyList<Volume> Volumes { get; }
    public IReadOnlyList<Chapter> AllChapters { get; }

    public BookDetail(Book book, IReadOnlyList<Volume> volumes, IReadOnlyList<Chapter> allChapters)
    {
        Book = book;
        Volumes = volumes;
        AllChapters = allChapters;
    }
}

/// <summary>导入结果</summary>
public sealed class BookImportResult
{
    public required Book Book { get; init; }
    public required string DetectedEncoding { get; init; }
    public required int VolumeCount { get; init; }
    public required int ChapterCount { get; init; }
}
