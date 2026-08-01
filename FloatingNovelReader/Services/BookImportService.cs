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

        // 1. 一次性读入字节（只读一遍，避免先采样检测再全文解码的双重 IO）
        var bytes = File.ReadAllBytes(filePath);

        // 2. 编码检测（容错：坏字节替换为 U+FFFD，不会导入失败）
        var encoding = _detector.Detect(bytes);
        Log.Debug("检测到编码 {Encoding} ({WebName})", encoding.EncodingName, encoding.WebName);

        // 3. 卷章解析：直接在字节流上扫行，偏移精确，
        //    不受容错解码（U+FFFD 替换）导致的重编码长度漂移影响
        var bomLength = _detector.GetPreambleLength(filePath, encoding);
        var book = _parser.Parse(bytes, filePath, encoding, bomLength);
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

        return book;
    }
}
