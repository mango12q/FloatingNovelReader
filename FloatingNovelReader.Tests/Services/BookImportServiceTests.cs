using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FloatingNovelReader.Core;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using FloatingNovelReader.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// BookImportService 端到端导入流程测试（TXT / EPUB / PDF 三种来源）。
/// </summary>
public class BookImportServiceTests : IDisposable
{
    private readonly string _dbFile;
    private readonly string _cacheDir;
    private readonly DatabaseService _db;
    private readonly BookImportService _importer;
    private readonly string _tmpFile;
    private readonly string _tmpDir;

    public BookImportServiceTests()
    {
        // 使用临时 DB 避免污染用户数据
        _dbFile = Path.Combine(Path.GetTempPath(), $"fnr_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbFile);
        _db.Initialize();

        // 正文缓存也要落在临时目录：EPUB/PDF 导入会往缓存目录写文件
        _cacheDir = Path.Combine(Path.GetTempPath(), $"fnr_import_cache_{Guid.NewGuid():N}");
        _tmpDir = Path.Combine(Path.GetTempPath(), $"fnr_import_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);

        var parser = new ChapterParser();
        _importer = new BookImportService(_db, parser, new ImportOptions { CacheDirectory = _cacheDir });

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

    [Fact]
    public async Task ImportAsync_Txt_LeavesContentPathEmpty()
    {
        var book = await _importer.ImportAsync(_tmpFile);

        // TXT 不生成缓存：章节偏移直接落在源文件上，阅读链路保持原样
        Assert.Null(book.ContentPath);
        Assert.Equal("txt", book.SourceFormat);
        Assert.Equal(_tmpFile, book.ResolveContentPath());
    }

    [Fact]
    public async Task ImportAsync_Epub_WritesCacheAndPersistsFormat()
    {
        var path = Path.Combine(_tmpDir, "novel.epub");
        File.WriteAllBytes(path, EbookFixtures.BuildEpub("百合故事", "としぞう", new[]
        {
            new EbookFixtures.EpubDocument("ch1.xhtml", "<h1>序言</h1><p>甲段</p>"),
            new EbookFixtures.EpubDocument("ch2.xhtml", "<h1>第一话</h1><p>乙段</p>"),
        }));

        var book = await _importer.ImportAsync(path);

        Assert.Equal("百合故事", book.Title);
        Assert.Equal("としぞう", book.Author);
        Assert.Equal("epub", book.SourceFormat);
        Assert.Equal(2, book.TotalChapters);
        Assert.NotNull(book.ContentPath);
        Assert.True(File.Exists(book.ContentPath));
        Assert.StartsWith(_cacheDir, book.ContentPath);

        // 落库后重新读出来：格式与缓存路径都要在（老库迁移列）
        var fromDb = _db.GetBook(book.Id)!;
        Assert.Equal("epub", fromDb.SourceFormat);
        Assert.Equal(book.ContentPath, fromDb.ContentPath);

        // 章节正文能从缓存读出来
        var full = new BookshelfService(_db, new ImportOptions { CacheDirectory = _cacheDir })
            .GetBookWithChapters(book.Id)!;
        var chapters = full.FlatChapters().ToList();
        Assert.Equal(2, chapters.Count);
        Assert.Contains("甲段", ChapterContentReader.Read(full, chapters[0]));
        Assert.Contains("乙段", ChapterContentReader.Read(full, chapters[1]));
    }

    [Fact]
    public async Task ImportAsync_Pdf_ExtractsTextAndChapters()
    {
        var path = Path.Combine(_tmpDir, "doc.pdf");
        File.WriteAllBytes(path, EbookFixtures.BuildPdf(new[]
        {
            new[] { "Chapter 1 Alpha", "Body of the first chapter." },
            new[] { "Chapter 2 Beta", "Body of the second chapter." },
        }));

        var book = await _importer.ImportAsync(path);

        Assert.Equal("pdf", book.SourceFormat);
        Assert.Equal(2, book.TotalChapters);
        Assert.Equal("Chapter 1 Alpha", book.FlatChapters().First().Title);
        Assert.Contains("Body of the first chapter.",
            ChapterContentReader.Read(book, book.FlatChapters().First()));
    }

    [Fact]
    public async Task ImportAsync_EpubWithWrongExtension_IsDetectedByMagicBytes()
    {
        var path = Path.Combine(_tmpDir, "mystery.bin");
        File.WriteAllBytes(path, EbookFixtures.BuildEpub("无扩展名", "作者", new[]
        {
            new EbookFixtures.EpubDocument("a.xhtml", "<h1>第一章</h1><p>正文</p>"),
        }));

        var book = await _importer.ImportAsync(path);

        Assert.Equal("epub", book.SourceFormat);
    }

    [Fact]
    public async Task ImportAsync_ScannedPdf_ThrowsWithActionableMessage()
    {
        var path = Path.Combine(_tmpDir, "scan.pdf");
        File.WriteAllBytes(path, EbookFixtures.BuildEmptyTextPdf());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _importer.ImportAsync(path));

        Assert.Contains("扫描版", ex.Message);
        // 失败不能留下半份记录
        Assert.Empty(_db.ListBooks());
    }

    [Fact]
    public async Task ImportAsync_UnsupportedFormat_Throws()
    {
        var path = Path.Combine(_tmpDir, "note.docx");
        File.WriteAllText(path, "not a book");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _importer.ImportAsync(path));

        Assert.Contains("TXT", ex.Message);
        Assert.Contains("EPUB", ex.Message);
        Assert.Contains("PDF", ex.Message);
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
        try { if (File.Exists(_tmpFile)) File.Delete(_tmpFile); } catch { }
        foreach (var dir in new[] { _cacheDir, _tmpDir })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
