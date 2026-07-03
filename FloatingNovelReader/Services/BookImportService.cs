using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// TXT 文件导入流程：
///   1. 选择 .txt 文件
///   2. 检测编码
///   3. 解码为字符串
///   4. 卷章解析
///   5. 写库
/// </summary>
public sealed class BookImportService
{
    private readonly DatabaseService _db;
    private readonly TextEncoderDetector _detector = new();
    private readonly ChapterParser _parser;

    public BookImportService(DatabaseService db, ChapterParser parser)
    {
        _db = db;
        _parser = parser;
    }

    public async Task<Book> ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("源文件不存在", filePath);

        return await Task.Run(() => Import(filePath));
    }

    public Book Import(string filePath)
    {
        Log.Information("开始导入 {File}", filePath);
        var fi = new FileInfo(filePath);

        // 1. 编码检测
        var encoding = _detector.DetectFromFile(filePath);
        Log.Debug("检测到编码 {Encoding} ({WebName})", encoding.EncodingName, encoding.WebName);

        // 2. 解码
        var text = _detector.DecodeFile(filePath, encoding);

        // 3. 卷章解析
        var book = _parser.Parse(text, filePath, fi.Length, encoding);
        book.Encoding = encoding.WebName ?? encoding.EncodingName;

        // 4. 入库
        var bookId = _db.InsertBook(book);
        book.Id = bookId;

        // 把内存中的 Volume/Chapter 重新写库（会回填 Id）
        _db.InsertVolumes(bookId, book.Volumes);

        // 更新总数
        _db.UpdateBookTotals(bookId, book.TotalChapters, book.TotalVolumes);

        // 5. 初始化阅读进度
        var firstChapter = book.FlatChapters().FirstOrDefault();
        if (firstChapter != null)
        {
            _db.SaveProgress(new ReadingProgress
            {
                BookId = bookId,
                ChapterId = firstChapter.Id,
                PageNumber = 0,
            });
        }

        Log.Information("导入完成 {Title} 卷数={Volumes} 章数={Chapters}",
            book.Title, book.TotalVolumes, book.TotalChapters);

        Core.EventBus.Default.Publish(Core.Constants.EvtBookImported, book);
        return book;
    }
}
