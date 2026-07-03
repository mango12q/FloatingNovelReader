using FloatingNovelReader.Models;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// 卷仓储接口。
/// </summary>
public interface IVolumeRepository
{
    /// <summary>插入多卷（含 Chapters，在同一个事务中完成，回填 Id）</summary>
    Task InsertManyAsync(int bookId, IReadOnlyList<Volume> volumes, CancellationToken ct = default);

    /// <summary>查询某书的所有卷（不含 Chapters）</summary>
    Task<IReadOnlyList<Volume>> GetByBookIdAsync(int bookId, CancellationToken ct = default);

    /// <summary>查询某卷的所有章节</summary>
    Task<IReadOnlyList<Chapter>> GetChaptersByVolumeIdAsync(int volumeId, CancellationToken ct = default);
}
