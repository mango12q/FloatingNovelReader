using FloatingNovelReader.Models;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// 阅读进度仓储接口。
/// </summary>
public interface IReadingProgressRepository
{
    /// <summary>保存进度（Upsert：按 BookId 存在则更新，不存在则插入）</summary>
    Task SaveAsync(ReadingProgress progress, CancellationToken ct = default);

    /// <summary>查询某书的阅读进度</summary>
    Task<ReadingProgress?> GetByBookIdAsync(int bookId, CancellationToken ct = default);

    /// <summary>查询最近一次阅读的进度</summary>
    Task<ReadingProgress?> GetMostRecentAsync(CancellationToken ct = default);
}
