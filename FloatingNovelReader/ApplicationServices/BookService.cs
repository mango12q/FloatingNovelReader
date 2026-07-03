using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FloatingNovelReader.Infrastructure;
using FloatingNovelReader.Infrastructure.Repositories;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.ApplicationServices;

/// <summary>
/// 书籍应用服务实现。编排多个 Repository 完成书架用例。
/// </summary>
public sealed class BookService : IBookService
{
    private readonly IDbConnectionFactory _factory;
    private readonly IBookRepository _books;
    private readonly IChapterRepository _chapters;
    private readonly IReadingProgressRepository _progress;

    // 缓存（书架列表）
    private List<Book>? _cachedBooks;

    public BookService(IDbConnectionFactory factory, IBookRepository books, IChapterRepository chapters, IReadingProgressRepository progress)
    {
        _factory = factory;
        _books = books;
        _chapters = chapters;
        _progress = progress;
    }

    public Task<IReadOnlyList<Book>> GetBookshelfAsync(string? search, string orderBy, CancellationToken ct = default)
    {
        return _books.ListAsync(search, orderBy, ct);
    }

    public Task<Book?> GetBookAsync(int bookId, CancellationToken ct = default)
    {
        return _books.GetByIdAsync(bookId, ct);
    }

    public async Task<BookDetail?> GetBookDetailAsync(int bookId, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(bookId, ct).ConfigureAwait(false);
        if (book == null) return null;

        // 并发查询 Volumes + Chapters，减少 IO 等待
        var volumesTask = Task.Run(async () =>
        {
            var vList = new List<Volume>();
            using var conn = _factory.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, VolumeNumber, Title, StartPosition, EndPosition FROM Volumes WHERE BookId=$id ORDER BY VolumeNumber;";
            cmd.Parameters.AddWithValue("$id", bookId);
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                vList.Add(new Volume
                {
                    Id = reader.GetInt32(0),
                    BookId = bookId,
                    VolumeNumber = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    StartPosition = reader.GetInt64(3),
                    EndPosition = reader.GetInt64(4),
                });
            }
            return (IReadOnlyList<Volume>)vList;
        }, ct);

        var chaptersTask = _chapters.GetByBookIdAsync(bookId, ct);

        await Task.WhenAll(volumesTask, chaptersTask).ConfigureAwait(false);

        var volumes = volumesTask.Result;
        var allChapters = chaptersTask.Result;

        // 把 Chapters 按 VolumeId 分组挂载到对应 Volume
        var chaptersByVolume = allChapters.GroupBy(c => c.VolumeId).ToDictionary(g => g.Key);
        foreach (var v in volumes)
        {
            if (chaptersByVolume.TryGetValue(v.Id, out var group))
                v.Chapters = group.ToList();
        }

        return new BookDetail(book, volumes, allChapters);
    }

    public Task<BookImportResult> ImportAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("源文件不存在", filePath);

        // 占位：后续由 BookImportPipeline（结合 ChapterParser + BookRepository）完成
        throw new NotImplementedException("BookService.ImportAsync 待后续迭代接入 BookImportPipeline");
    }

    public async Task RemoveAsync(int bookId, bool deleteSourceFile, CancellationToken ct = default)
    {
        if (deleteSourceFile)
        {
            var book = await _books.GetByIdAsync(bookId, ct).ConfigureAwait(false);
            if (book != null && !string.IsNullOrEmpty(book.FilePath) && File.Exists(book.FilePath))
            {
                File.Delete(book.FilePath);
                Log.Information("已删除源文件 {Path}", book.FilePath);
            }
        }
        await _books.DeleteAsync(bookId, ct).ConfigureAwait(false);
    }

    public async Task ReloadAsync(string? search = null, string orderBy = "LastReadTime", CancellationToken ct = default)
    {
        _cachedBooks = (List<Book>?)await _books.ListAsync(search, orderBy, ct).ConfigureAwait(false);
    }

    public IReadOnlyList<Book> GetCachedBooks() => _cachedBooks ??= new List<Book>();
}
