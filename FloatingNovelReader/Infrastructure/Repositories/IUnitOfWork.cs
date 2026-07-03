using FloatingNovelReader.Models;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// 工作单元接口。协调多个 Repository 在同一个事务中完成原子性操作。
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IBookRepository Books { get; }
    IChapterRepository Chapters { get; }
    IBookmarkRepository Bookmarks { get; }
    IReadingProgressRepository ReadingProgress { get; }

    /// <summary>提交事务</summary>
    Task<int> CommitAsync(CancellationToken ct = default);
}
