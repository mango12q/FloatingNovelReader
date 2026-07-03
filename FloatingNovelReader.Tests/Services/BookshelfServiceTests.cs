using System;
using System.IO;
using System.Linq;
using System.Text;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// BookshelfService 端到端测试: 重点是 Remove 时 CASCADE 是否清空
/// Volumes / Chapters / ReadingProgress / Bookmarks。
/// </summary>
public class BookshelfServiceTests : IDisposable
{
    private readonly string _dbFile;
    private readonly DatabaseService _db;
    private readonly BookshelfService _bookshelf;
    private readonly BookImportService _importer;
    private readonly string _tmpFile;

    public BookshelfServiceTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"fnr_bshelf_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbFile);
        _db.Initialize();
        _bookshelf = new BookshelfService(_db);
        _importer = new BookImportService(_db, new ChapterParser());

        _tmpFile = Path.Combine(Path.GetTempPath(), $"demo_{Guid.NewGuid():N}.txt");
        File.WriteAllText(_tmpFile,
            "第一章 开端\n内容A\n第二章 发展\n内容B\n第三章 高潮\n内容C\n",
            Encoding.UTF8);
    }

    [Fact]
    public async System.Threading.Tasks.Task Remove_WithDeleteFile_RemovesEverything()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        // 加一个书签 + 写一段进度
        _db.AddBookmark(new Bookmark { BookId = book.Id, ChapterId = 1, PageNumber = 0 });
        Assert.NotEmpty(_db.GetChapters(book.Id));
        Assert.NotEmpty(_db.ListBookmarks(book.Id));

        // 删书+源文件
        var deletedFile = _bookshelf.Remove(book.Id, deleteSourceFile: true);

        // 1) 源文件没了
        Assert.Equal(_tmpFile, deletedFile);
        Assert.False(File.Exists(_tmpFile));

        // 2) Books 记录没了
        Assert.Empty(_db.ListBooks());

        // 3) CASCADE 生效: Volumes / Chapters / ReadingProgress / Bookmarks 都没了
        Assert.Empty(_db.GetVolumes(book.Id));
        Assert.Empty(_db.GetChapters(book.Id));
        Assert.Empty(_db.ListBookmarks(book.Id));
        Assert.Null(_db.GetProgress(book.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Remove_WithoutDeleteFile_KeepsFile()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        var deletedFile = _bookshelf.Remove(book.Id, deleteSourceFile: false);
        Assert.Null(deletedFile);
        Assert.True(File.Exists(_tmpFile));
        Assert.Empty(_db.ListBooks());
    }

    [Fact]
    public async System.Threading.Tasks.Task Remove_BackwardCompat_OldSignature_KeepsFile()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        // 不传 deleteSourceFile 参数的旧调用, 应该等价于 deleteSourceFile=false
        _bookshelf.Remove(book.Id);
        Assert.True(File.Exists(_tmpFile));
        Assert.Empty(_db.ListBooks());
    }

    [Fact]
    public async System.Threading.Tasks.Task GetBookWithChapters_ReturnsFullTree()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        _bookshelf.Reload();
        var full = _bookshelf.GetBookWithChapters(book.Id);
        Assert.NotNull(full);
        Assert.NotEmpty(full!.Volumes);
        Assert.NotEmpty(full.Volumes[0].Chapters);
        Assert.Contains(full.Volumes[0].Chapters, c => c.Title.StartsWith("第"));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetBookWithChapters_AfterDelete_ReturnsNull()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        _bookshelf.Reload();
        _bookshelf.Remove(book.Id);
        Assert.Null(_bookshelf.GetBookWithChapters(book.Id));
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
        try { if (File.Exists(_tmpFile)) File.Delete(_tmpFile); } catch { }
    }
}
