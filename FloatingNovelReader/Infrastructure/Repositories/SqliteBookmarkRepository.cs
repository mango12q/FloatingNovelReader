using FloatingNovelReader.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// SQLite 实现书签仓储。
/// </summary>
public sealed class SqliteBookmarkRepository : IBookmarkRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqliteBookmarkRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> AddAsync(Bookmark bookmark, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Bookmarks (BookId, ChapterId, PageNumber, Note, CreatedTime)
VALUES ($bookId, $chapterId, $page, $note, $time);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$bookId", bookmark.BookId);
        cmd.Parameters.AddWithValue("$chapterId", bookmark.ChapterId);
        cmd.Parameters.AddWithValue("$page", bookmark.PageNumber);
        cmd.Parameters.AddWithValue("$note", (object?)bookmark.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$time", bookmark.CreatedTime.ToString("o"));
        var id = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        return (int)id;
    }

    public async Task DeleteAsync(int bookmarkId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Bookmarks WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", bookmarkId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Bookmark>> GetByBookIdAsync(int bookId, CancellationToken ct = default)
    {
        var list = new List<Bookmark>();
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Bookmarks WHERE BookId=$id ORDER BY CreatedTime DESC;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Bookmark
            {
                Id = reader.GetInt32(0),
                BookId = reader.GetInt32(1),
                ChapterId = reader.GetInt32(2),
                PageNumber = reader.GetInt32(3),
                Note = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedTime = DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }
        return list;
    }
}
