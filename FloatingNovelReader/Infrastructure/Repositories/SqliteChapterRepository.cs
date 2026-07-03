using FloatingNovelReader.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// SQLite 实现章节仓储。
/// </summary>
public sealed class SqliteChapterRepository : IChapterRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqliteChapterRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Chapter>> GetByBookIdAsync(int bookId, CancellationToken ct = default)
    {
        var list = new List<Chapter>();
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, BookId, VolumeId, ChapterNumber, DisplayNumber, Title, StartPosition, EndPosition, StartLineNumber, LineCount FROM Chapters WHERE BookId=$id ORDER BY VolumeId, ChapterNumber;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Chapter
            {
                Id = reader.GetInt32(0),
                BookId = reader.GetInt32(1),
                VolumeId = reader.GetInt32(2),
                ChapterNumber = reader.GetInt32(3),
                DisplayNumber = reader.GetInt32(4),
                Title = reader.GetString(5),
                StartPosition = reader.GetInt64(6),
                EndPosition = reader.GetInt64(7),
                StartLineNumber = reader.GetInt32(8),
                LineCount = reader.GetInt32(9),
            });
        }
        return list;
    }

    public async Task<Chapter?> GetByIdAsync(int chapterId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Chapters WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", chapterId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Chapter
        {
            Id = reader.GetInt32(0),
            BookId = reader.GetInt32(1),
            VolumeId = reader.GetInt32(2),
            ChapterNumber = reader.GetInt32(3),
            DisplayNumber = reader.GetInt32(4),
            Title = reader.GetString(5),
            StartPosition = reader.GetInt64(6),
            EndPosition = reader.GetInt64(7),
            StartLineNumber = reader.GetInt32(8),
            LineCount = reader.GetInt32(9),
        };
    }

    public async Task InsertManyAsync(int bookId, int volumeId, IReadOnlyList<Chapter> chapters, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var ch in chapters)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Chapters (BookId, VolumeId, ChapterNumber, DisplayNumber, Title, StartPosition, EndPosition, StartLineNumber, LineCount)
VALUES ($bookId, $volId, $chNum, $dispNum, $title, $start, $end, $line, $count);
SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$bookId", bookId);
                cmd.Parameters.AddWithValue("$volId", volumeId);
                cmd.Parameters.AddWithValue("$chNum", ch.ChapterNumber);
                cmd.Parameters.AddWithValue("$dispNum", ch.DisplayNumber);
                cmd.Parameters.AddWithValue("$title", ch.Title);
                cmd.Parameters.AddWithValue("$start", ch.StartPosition);
                cmd.Parameters.AddWithValue("$end", ch.EndPosition);
                cmd.Parameters.AddWithValue("$line", ch.StartLineNumber);
                cmd.Parameters.AddWithValue("$count", ch.LineCount);
                var chId = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
                ch.Id = (int)chId;
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
