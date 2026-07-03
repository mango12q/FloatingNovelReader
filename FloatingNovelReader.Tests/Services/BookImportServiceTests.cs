using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// BookImportService 端到端导入流程测试。
/// </summary>
public class BookImportServiceTests : IDisposable
{
    private readonly string _dbFile;
    private readonly DatabaseService _db;
    private readonly BookImportService _importer;
    private readonly string _tmpFile;

    public BookImportServiceTests()
    {
        // 使用临时 DB 避免污染用户数据
        _dbFile = Path.Combine(Path.GetTempPath(), $"fnr_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbFile);
        _db.Initialize();
        var parser = new ChapterParser();
        _importer = new BookImportService(_db, parser);

        // 准备临时 TXT
        _tmpFile = Path.Combine(Path.GetTempPath(), $"三体_{Guid.NewGuid():N}.txt");
        var text = "三体\n作者：刘慈欣\n\n第一卷 地球往事\n\n第一章 科学边界\n内容A\n\n第二章 台球\n内容B\n\n第二卷 黑暗森林\n\n第三章 射手\n内容C\n";
        File.WriteAllText(_tmpFile, text, Encoding.UTF8);
    }

    [Fact]
    public async Task ImportAsync_BasicBook_Succeeds()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        Assert.True(book.Id > 0);
        Assert.StartsWith("三体", book.Title);
        Assert.Equal("刘慈欣", book.Author);
        Assert.True(book.TotalChapters >= 3);
    }

    [Fact]
    public async Task ImportAsync_Then_ListFromDb()
    {
        var book = await _importer.ImportAsync(_tmpFile);
        var list = _db.ListBooks();
        Assert.Single(list);
        Assert.StartsWith("三体", list[0].Title);
    }

    [Fact]
    public async Task ImportAsync_Duplicate_Throws()
    {
        await _importer.ImportAsync(_tmpFile);
        await Assert.ThrowsAsync<SqliteException>(async () => await _importer.ImportAsync(_tmpFile));
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
        try { if (File.Exists(_tmpFile)) File.Delete(_tmpFile); } catch { }
    }
}
