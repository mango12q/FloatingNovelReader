using FloatingNovelReader.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;

namespace FloatingNovelReader.Infrastructure;

/// <summary>
/// SQLite 实现 UnitOfWork。在同一个 SqliteConnection + Transaction 上协调多个 Repository。
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _factory;
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;

    public IBookRepository Books { get; }
    public IChapterRepository Chapters { get; }
    public IBookmarkRepository Bookmarks { get; }
    public IReadingProgressRepository ReadingProgress { get; }

    public SqliteUnitOfWork(IDbConnectionFactory factory)
    {
        _factory = factory;
        _connection = factory.CreateConnection();
        _transaction = _connection.BeginTransaction();

        Books = new SqliteBookRepository(factory);         // 事务感知在具体方法中通过 tx 参数传递
        Chapters = new SqliteChapterRepository(factory);
        Bookmarks = new SqliteBookmarkRepository(factory);
        ReadingProgress = new SqliteReadingProgressRepository(factory);
    }

    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        try
        {
            await _transaction.CommitAsync(ct);
            return 0;
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task RollbackAsync()
    {
        try { await _transaction.RollbackAsync(); } catch { /* ignore */ }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _transaction.DisposeAsync(); } catch { }
        try { await _connection.DisposeAsync(); } catch { }
    }
}
