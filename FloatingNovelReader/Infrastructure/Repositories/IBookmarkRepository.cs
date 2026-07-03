using FloatingNovelReader.Models;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// 书签仓储接口。
/// </summary>
public interface IBookmarkRepository
{
    /// <summary>添加书签，返回自增 Id</summary>
    Task<int> AddAsync(Bookmark bookmark, CancellationToken ct = default);

    /// <summary>删除书签</summary>
    Task DeleteAsync(int bookmarkId, CancellationToken ct = default);

    /// <summary>查询某书的所有书签（按创建时间倒序）</summary>
    Task<IReadOnlyList<Bookmark>> GetByBookIdAsync(int bookId, CancellationToken ct = default);
}
