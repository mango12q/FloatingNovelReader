using System;
using System.Collections.Generic;
using System.IO;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// SQLite 数据库管理：
///   - Initialize()：建表
///   - Books / Volumes / Chapters / ReadingProgress / Bookmarks 五个表的 CRUD
/// 使用参数化 SQL 防注入。
/// </summary>
public sealed class DatabaseService
{
    private readonly string _dbPath;

    // Default Timeout: SQLite 单写者模型下，并发写默认立即抛 SQLITE_BUSY，
    // 设置 busy timeout 后改为等待重试（进度防抖保存与后台读并发时尤其需要）
    private string ConnectionString => $"Data Source={_dbPath};Default Timeout=2";

    public DatabaseService() : this(Constants.DbFile) { }

    public DatabaseService(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void Initialize()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var conn = OpenConnection();
        // 关键: SQLite 默认不开启外键约束；不开的话 ON DELETE CASCADE 形同虚设
        ExecNonQuery(conn, "PRAGMA foreign_keys = ON;");

        ExecNonQuery(conn, @"
CREATE TABLE IF NOT EXISTS Books (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    Author TEXT,
    FilePath TEXT NOT NULL UNIQUE,
    FileSize INTEGER,
    Encoding TEXT,
    TotalChapters INTEGER DEFAULT 0,
    TotalVolumes INTEGER DEFAULT 0,
    ImportTime TEXT NOT NULL,
    LastReadTime TEXT,
    CoverColor TEXT DEFAULT '#6C8CFF'
);");

        ExecNonQuery(conn, @"
CREATE TABLE IF NOT EXISTS Volumes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BookId INTEGER NOT NULL REFERENCES Books(Id) ON DELETE CASCADE,
    VolumeNumber INTEGER NOT NULL,
    Title TEXT NOT NULL,
    StartPosition INTEGER NOT NULL,
    EndPosition INTEGER NOT NULL
);");

        ExecNonQuery(conn, @"
CREATE TABLE IF NOT EXISTS Chapters (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BookId INTEGER NOT NULL REFERENCES Books(Id) ON DELETE CASCADE,
    VolumeId INTEGER NOT NULL REFERENCES Volumes(Id) ON DELETE CASCADE,
    ChapterNumber INTEGER NOT NULL,
    DisplayNumber INTEGER NOT NULL,
    Title TEXT NOT NULL,
    StartPosition INTEGER NOT NULL,
    EndPosition INTEGER NOT NULL,
    StartLineNumber INTEGER,
    LineCount INTEGER
);");

        ExecNonQuery(conn, @"
CREATE TABLE IF NOT EXISTS ReadingProgress (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BookId INTEGER NOT NULL UNIQUE REFERENCES Books(Id) ON DELETE CASCADE,
    ChapterId INTEGER NOT NULL,
    PageNumber INTEGER NOT NULL DEFAULT 0,
    LastUpdated TEXT NOT NULL,
    WindowLeft REAL,
    WindowTop REAL,
    WindowWidth REAL,
    WindowHeight REAL,
    Opacity REAL DEFAULT 1.0
);");

        ExecNonQuery(conn, @"
CREATE TABLE IF NOT EXISTS Bookmarks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BookId INTEGER NOT NULL REFERENCES Books(Id) ON DELETE CASCADE,
    ChapterId INTEGER NOT NULL,
    PageNumber INTEGER NOT NULL,
    Note TEXT,
    CreatedTime TEXT NOT NULL
);");

        // 索引
        ExecNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_chapters_book ON Chapters(BookId);");
        ExecNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_volumes_book ON Volumes(BookId);");
        ExecNonQuery(conn, "CREATE INDEX IF NOT EXISTS idx_bookmarks_book ON Bookmarks(BookId);");

        Log.Information("SQLite 初始化完成 {Path}", _dbPath);
    }

    // -------- Books --------

    public int InsertBook(Book book)
    {
        using var conn = OpenConnection();
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
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return (int)id;
    }

    public void UpdateBookTotals(int bookId, int totalChapters, int totalVolumes)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Books SET TotalChapters=$tc, TotalVolumes=$tv WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$tc", totalChapters);
        cmd.Parameters.AddWithValue("$tv", totalVolumes);
        cmd.Parameters.AddWithValue("$id", bookId);
        cmd.ExecuteNonQuery();
    }

    public void TouchLastReadTime(int bookId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Books SET LastReadTime=$t WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", bookId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteBook(int bookId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Books WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", bookId);
        cmd.ExecuteNonQuery();
    }

    public List<Book> ListBooks(string? search = null, string orderBy = "LastReadTime")
    {
        var list = new List<Book>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT * FROM Books";
        if (!string.IsNullOrWhiteSpace(search))
            sql += " WHERE Title LIKE $s ESCAPE '\\'";
        sql += orderBy switch
        {
            "Title" => " ORDER BY Title COLLATE NOCASE ASC",
            "ImportTime" => " ORDER BY ImportTime DESC",
            _ => " ORDER BY LastReadTime DESC NULLS LAST, ImportTime DESC"
        };
        cmd.CommandText = sql;
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("$s", "%" + EscapeLike(search) + "%");
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(MapBook(reader));
        return list;
    }

    public Book? GetBook(int bookId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Books WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return MapBook(reader);
    }

    private static string EscapeLike(string s)
        => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static Book MapBook(SqliteDataReader reader)
    {
        return new Book
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            Author = reader.IsDBNull(2) ? null : reader.GetString(2),
            FilePath = reader.GetString(3),
            FileSize = reader.GetInt64(4),
            Encoding = reader.IsDBNull(5) ? "utf-8" : reader.GetString(5),
            TotalChapters = reader.GetInt32(6),
            TotalVolumes = reader.GetInt32(7),
            ImportTime = DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
            LastReadTime = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
            CoverColor = reader.GetString(10),
        };
    }

    // -------- Volumes --------

    public void InsertVolumes(int bookId, IEnumerable<Volume> volumes)
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
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
            var id = (long)(cmd.ExecuteScalar() ?? 0L);
            v.Id = (int)id;

            // 插入章节
            foreach (var ch in v.Chapters)
            {
                using var ccmd = conn.CreateCommand();
                ccmd.Transaction = tx;
                ccmd.CommandText = @"
INSERT INTO Chapters (BookId, VolumeId, ChapterNumber, DisplayNumber, Title, StartPosition, EndPosition, StartLineNumber, LineCount)
VALUES ($bookId, $volId, $chNum, $dispNum, $title, $start, $end, $line, $count);
SELECT last_insert_rowid();";
                ccmd.Parameters.AddWithValue("$bookId", bookId);
                ccmd.Parameters.AddWithValue("$volId", v.Id);
                ccmd.Parameters.AddWithValue("$chNum", ch.ChapterNumber);
                ccmd.Parameters.AddWithValue("$dispNum", ch.DisplayNumber);
                ccmd.Parameters.AddWithValue("$title", ch.Title);
                ccmd.Parameters.AddWithValue("$start", ch.StartPosition);
                ccmd.Parameters.AddWithValue("$end", ch.EndPosition);
                ccmd.Parameters.AddWithValue("$line", ch.StartLineNumber);
                ccmd.Parameters.AddWithValue("$count", ch.LineCount);
                var chId = (long)(ccmd.ExecuteScalar() ?? 0L);
                ch.Id = (int)chId;
                ch.VolumeId = v.Id;
                ch.BookId = bookId;
            }
        }
        tx.Commit();
    }

    public List<Volume> GetVolumes(int bookId)
    {
        var list = new List<Volume>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, VolumeNumber, Title, StartPosition, EndPosition FROM Volumes WHERE BookId=$id ORDER BY VolumeNumber;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
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

    public List<Chapter> GetChapters(int bookId)
    {
        var list = new List<Chapter>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, VolumeId, ChapterNumber, DisplayNumber, Title, StartPosition, EndPosition, StartLineNumber, LineCount FROM Chapters WHERE BookId=$id ORDER BY VolumeId, ChapterNumber;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Chapter
            {
                Id = reader.GetInt32(0),
                BookId = bookId,
                VolumeId = reader.GetInt32(1),
                ChapterNumber = reader.GetInt32(2),
                DisplayNumber = reader.GetInt32(3),
                Title = reader.GetString(4),
                StartPosition = reader.GetInt64(5),
                EndPosition = reader.GetInt64(6),
                StartLineNumber = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                LineCount = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
            });
        }
        return list;
    }

    public Chapter? GetChapter(int chapterId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Chapters WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", chapterId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
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
            StartLineNumber = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
            LineCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
        };
    }

    // -------- ReadingProgress --------

    public void SaveProgress(ReadingProgress p)
    {
        using var conn = OpenConnection();
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
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 只保存阅读位置（章节/页码/时间），不触碰窗口几何列。
    /// 翻页防抖保存走这里，避免读-改-写把窗口几何污染成默认值。
    /// </summary>
    public void SaveReadingPosition(int bookId, int chapterId, int pageNumber, DateTime lastUpdated)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO ReadingProgress (BookId, ChapterId, PageNumber, LastUpdated)
VALUES ($bookId, $chapterId, $page, $time)
ON CONFLICT(BookId) DO UPDATE SET
    ChapterId = excluded.ChapterId,
    PageNumber = excluded.PageNumber,
    LastUpdated = excluded.LastUpdated;";
        cmd.Parameters.AddWithValue("$bookId", bookId);
        cmd.Parameters.AddWithValue("$chapterId", chapterId);
        cmd.Parameters.AddWithValue("$page", pageNumber);
        cmd.Parameters.AddWithValue("$time", lastUpdated.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 只保存窗口几何（位置/尺寸/透明度），不触碰章节/页码列。
    /// 窗口关闭时调用；进度行不存在时（尚未翻页）忽略，几何由下次完整保存写入。
    /// </summary>
    public void SaveWindowGeometry(int bookId, double left, double top, double width, double height, double opacity)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE ReadingProgress SET
    WindowLeft = $left,
    WindowTop = $top,
    WindowWidth = $width,
    WindowHeight = $height,
    Opacity = $opacity
WHERE BookId = $bookId;";
        cmd.Parameters.AddWithValue("$bookId", bookId);
        cmd.Parameters.AddWithValue("$left", left);
        cmd.Parameters.AddWithValue("$top", top);
        cmd.Parameters.AddWithValue("$width", width);
        cmd.Parameters.AddWithValue("$height", height);
        cmd.Parameters.AddWithValue("$opacity", opacity);
        cmd.ExecuteNonQuery();
    }

    public ReadingProgress? GetProgress(int bookId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ReadingProgress WHERE BookId=$id;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return MapProgress(reader);
    }

    public ReadingProgress? GetMostRecentProgress()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ReadingProgress ORDER BY LastUpdated DESC LIMIT 1;";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return MapProgress(reader);
    }

    private static ReadingProgress MapProgress(SqliteDataReader reader)
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
            WindowWidth = reader.IsDBNull(7) ? Constants.DefaultWidth : reader.GetDouble(7),
            WindowHeight = reader.IsDBNull(8) ? Constants.DefaultHeight : reader.GetDouble(8),
            Opacity = reader.IsDBNull(9) ? 1 : reader.GetDouble(9),
        };
    }

    // -------- Bookmarks --------

    public int AddBookmark(Bookmark b)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Bookmarks (BookId, ChapterId, PageNumber, Note, CreatedTime)
VALUES ($bookId, $chapterId, $page, $note, $time);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$bookId", b.BookId);
        cmd.Parameters.AddWithValue("$chapterId", b.ChapterId);
        cmd.Parameters.AddWithValue("$page", b.PageNumber);
        cmd.Parameters.AddWithValue("$note", (object?)b.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$time", b.CreatedTime.ToString("o"));
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        return (int)id;
    }

    public void DeleteBookmark(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Bookmarks WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<Bookmark> ListBookmarks(int bookId)
    {
        var list = new List<Bookmark>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Bookmarks WHERE BookId=$id ORDER BY CreatedTime DESC;";
        cmd.Parameters.AddWithValue("$id", bookId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
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

    // -------- Util --------

    /// <summary>
    /// 打开一个 Sqlite 连接并自动开启外键约束。
    /// 必须用此方法而非直接 new SqliteConnection，否则 ON DELETE CASCADE 不会生效。
    /// </summary>
    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(ConnectionString);
        try
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    private static void ExecNonQuery(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
