using FloatingNovelReader.Models;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// 书籍仓储接口。定义对 Books 表的所有操作。
/// </summary>
public interface IBookRepository
{
    /// <summary>插入一本新书，返回自增 Id</summary>
    Task<int> InsertAsync(Book book, CancellationToken ct = default);

    /// <summary>根据 Id 查询书籍元数据（不含 Volumes/Chapters）</summary>
    Task<Book?> GetByIdAsync(int bookId, CancellationToken ct = default);

    /// <summary>查询书架列表（支持搜索和排序）</summary>
    Task<IReadOnlyList<Book>> ListAsync(string? search, string orderBy, CancellationToken ct = default);

    /// <summary>删除一本书及其级联数据</summary>
    Task DeleteAsync(int bookId, CancellationToken ct = default);

    /// <summary>更新书籍的章/卷总数</summary>
    Task UpdateTotalsAsync(int bookId, int totalChapters, int totalVolumes, CancellationToken ct = default);

    /// <summary>更新最后阅读时间</summary>
    Task TouchLastReadTimeAsync(int bookId, CancellationToken ct = default);
}
