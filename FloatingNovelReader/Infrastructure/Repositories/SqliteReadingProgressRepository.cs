using FloatingNovelReader.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// SQLite 实现阅读进度仓储。
/// </summary>
public sealed class SqliteReadingProgressRepository : IReadingProgressRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqliteReadingProgressRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task SaveAsync(ReadingProgress p, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO ReadingProgress (BookId, ChapterId, PageNumber, LastUpdated, WindowLeft, WindowTop, WindowWidth, WindowHeight, Opacity)
VALUES ($bookId, $chapterId, $page, $time, $left, $top, $width, $height, $opacity)
ON CONFLICT(BookId) DO UPDATE SET
    ChapterId = excluded.ChapterId,
    PageNumber = excluded.PageNumber,
    LastUpdated = excluded.LastUpdated,
    WindowLeft = excluded.WindowLeft,
    WindowTop = excluded.WindowTop,
    WindowWidth = excluded.WindowWidth,
    WindowHeight = excluded.WindowHeight,
    Opacity = excluded.Opacity;";
        cmd.Parameters.AddWithValue("$bookId", p.BookId);
        cmd.Parameters.AddWithValue("$chapterId", p.ChapterId);
        cmd.Parameters.AddWithValue("$page", p.PageNumber);
        cmd.Parameters.AddWithValue("$time", p.LastUpdated.ToString("o"));
        cmd.Parameters.AddWithValue("$left", p.WindowLeft);
        cmd.Parameters.AddWithValue("$top", p.WindowTop);
        cmd.Parameters.AddWithValue("$width", p.WindowWidth);
        cmd.Parameters.AddWithValue("$height", p.WindowHeight);
        cmd.Parameters.AddWithValue("$opacity", p.Opacity);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ReadingProgress?> GetByBookIdAsync(int bookId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ReadingProgress WHERE BookId=$id;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Map(reader);
    }

    public async Task<ReadingProgress?> GetMostRecentAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ReadingProgress ORDER BY LastUpdated DESC LIMIT 1;";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Map(reader);
    }

    private static ReadingProgress Map(SqliteDataReader reader)
    {
        return new ReadingProgress
        {
            Id = reader.GetInt32(0),
            BookId = reader.GetInt32(1),
            ChapterId = reader.GetInt32(2),
            PageNumber = reader.GetInt32(3),
            LastUpdated = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
            WindowLeft = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
            WindowTop = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
            WindowWidth = reader.IsDBNull(7) ? 500 : reader.GetDouble(7),
            WindowHeight = reader.IsDBNull(8) ? 700 : reader.GetDouble(8),
            Opacity = reader.IsDBNull(9) ? 1 : reader.GetDouble(9),
        };
    }
}
