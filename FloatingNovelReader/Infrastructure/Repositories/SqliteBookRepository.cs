using System.Data;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// SQLite 实现书籍仓储。
/// </summary>
public sealed class SqliteBookRepository : IBookRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqliteBookRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<int> InsertAsync(Book book, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Books (Title, Author, FilePath, FileSize, Encoding, TotalChapters, TotalVolumes, ImportTime, LastReadTime, CoverColor)
VALUES ($title, $author, $filePath, $fileSize, $encoding, $totalChapters, $totalVolumes, $importTime, $lastReadTime, $coverColor);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$title", book.Title);
        cmd.Parameters.AddWithValue("$author", (object?)book.Author ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$filePath", book.FilePath);
        cmd.Parameters.AddWithValue("$fileSize", book.FileSize);
        cmd.Parameters.AddWithValue("$encoding", book.Encoding);
        cmd.Parameters.AddWithValue("$totalChapters", book.TotalChapters);
        cmd.Parameters.AddWithValue("$totalVolumes", book.TotalVolumes);
        cmd.Parameters.AddWithValue("$importTime", book.ImportTime.ToString("o"));
        cmd.Parameters.AddWithValue("$lastReadTime", DBNull.Value);
        cmd.Parameters.AddWithValue("$coverColor", book.CoverColor);
        var id = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        return (int)id;
    }

    public async Task<Book?> GetByIdAsync(int bookId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Books WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return Map(reader);
    }

    public async Task<IReadOnlyList<Book>> ListAsync(string? search, string orderBy, CancellationToken ct = default)
    {
        var list = new List<Book>();
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT * FROM Books";
        if (!string.IsNullOrWhiteSpace(search))
            sql += " WHERE Title LIKE $s";
        sql += orderBy switch
        {
            "Title" => " ORDER BY Title COLLATE NOCASE ASC",
            "ImportTime" => " ORDER BY ImportTime DESC",
            _ => " ORDER BY LastReadTime DESC NULLS LAST, ImportTime DESC"
        };
        cmd.CommandText = sql;
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("$s", "%" + search + "%");
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task DeleteAsync(int bookId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Books WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", bookId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateTotalsAsync(int bookId, int totalChapters, int totalVolumes, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Books SET TotalChapters=$tc, TotalVolumes=$tv WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$tc", totalChapters);
        cmd.Parameters.AddWithValue("$tv", totalVolumes);
        cmd.Parameters.AddWithValue("$id", bookId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task TouchLastReadTimeAsync(int bookId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Books SET LastReadTime=$t WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", bookId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Book Map(SqliteDataReader reader)
    {
        return new Book
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            Author = reader.IsDBNull(2) ? null : reader.GetString(2),
            FilePath = reader.GetString(3),
            FileSize = reader.GetInt64(4),
            Encoding = reader.GetString(5),
            TotalChapters = reader.GetInt32(6),
            TotalVolumes = reader.GetInt32(7),
            ImportTime = DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
            LastReadTime = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
            CoverColor = reader.GetString(10),
        };
    }
}
