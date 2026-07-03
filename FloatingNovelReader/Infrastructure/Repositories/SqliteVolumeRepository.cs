using FloatingNovelReader.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FloatingNovelReader.Infrastructure.Repositories;

/// <summary>
/// SQLite 实现卷+章节仓储。
/// </summary>
public sealed class SqliteVolumeRepository : IVolumeRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqliteVolumeRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task InsertManyAsync(int bookId, IReadOnlyList<Volume> volumes, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var v in volumes)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO Volumes (BookId, VolumeNumber, Title, StartPosition, EndPosition)
VALUES ($bookId, $volNum, $title, $start, $end);
SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$bookId", bookId);
                cmd.Parameters.AddWithValue("$volNum", v.VolumeNumber);
                cmd.Parameters.AddWithValue("$title", v.Title);
                cmd.Parameters.AddWithValue("$start", v.StartPosition);
                cmd.Parameters.AddWithValue("$end", v.EndPosition);
                var id = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
                v.Id = (int)id;
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<Volume>> GetByBookIdAsync(int bookId, CancellationToken ct = default)
    {
        var list = new List<Volume>();
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, VolumeNumber, Title, StartPosition, EndPosition FROM Volumes WHERE BookId=$id ORDER BY VolumeNumber;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Volume
            {
                Id = reader.GetInt32(0),
                BookId = bookId,
                VolumeNumber = reader.GetInt32(1),
                Title = reader.GetString(2),
                StartPosition = reader.GetInt64(3),
                EndPosition = reader.GetInt64(4),
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<Chapter>> GetChaptersByVolumeIdAsync(int volumeId, CancellationToken ct = default)
    {
        var list = new List<Chapter>();
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, VolumeId, ChapterNumber, DisplayNumber, Title, StartPosition, EndPosition, StartLineNumber, LineCount FROM Chapters WHERE VolumeId=$id ORDER BY ChapterNumber;";
        cmd.Parameters.AddWithValue("$id", volumeId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Chapter
            {
                Id = reader.GetInt32(0),
                VolumeId = reader.GetInt32(1),
                ChapterNumber = reader.GetInt32(2),
                DisplayNumber = reader.GetInt32(3),
                Title = reader.GetString(4),
                StartPosition = reader.GetInt64(5),
                EndPosition = reader.GetInt64(6),
                StartLineNumber = reader.GetInt32(7),
                LineCount = reader.GetInt32(8),
            });
        }
        return list;
    }
}
