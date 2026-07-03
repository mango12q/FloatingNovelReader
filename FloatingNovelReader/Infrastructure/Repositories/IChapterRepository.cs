using FloatingNovelReader.Models;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// 章节仓储接口。
/// </summary>
public interface IChapterRepository
{
    /// <summary>查询某书所有章节（按 VolumeId, ChapterNumber 排序）</summary>
    Task<IReadOnlyList<Chapter>> GetByBookIdAsync(int bookId, CancellationToken ct = default);

    /// <summary>根据 Id 查询单个章节</summary>
    Task<Chapter?> GetByIdAsync(int chapterId, CancellationToken ct = default);

    /// <summary>批量插入章节（配合 IVolumeRepository 在事务中使用）</summary>
    Task InsertManyAsync(int bookId, int volumeId, IReadOnlyList<Chapter> chapters, CancellationToken ct = default);
}
