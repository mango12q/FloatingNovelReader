using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FloatingNovelReader.Core;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 导入流程。按来源格式分流：
///   TXT        ：检测编码 → 解码 → 卷章解析（偏移直接落在源文件上）
///   EPUB / PDF ：解析成正文 → 写 UTF-8 正文缓存 → 章节偏移指向缓存
/// 两条路径最终都落成同一套 Book/Volume/Chapter 结构，阅读侧无需区分格式。
/// </summary>
public sealed class BookImportService
{
    private readonly DatabaseService _db;
    private readonly TextEncoderDetector _detector = new();
    private readonly ChapterParser _parser;
    private readonly ImportOptions _options;

    public BookImportService(DatabaseService db, ChapterParser parser, ImportOptions options)
    {
        _db = db;
        _parser = parser;
        _options = options;
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

        if (!File.Exists(filePath))
            throw new FileNotFoundException("源文件不存在", filePath);

        var format = BookFormatDetector.Detect(filePath)
            ?? throw new InvalidOperationException("不支持的文件格式：目前支持 TXT / EPUB / PDF。");

        // 先做大小守卫：整份文件会同时驻留托管堆（大对象堆），解析器还会再复制一份，
        // 没有上限时一个超大文件就能把进程撑爆，且 OOM 的报错完全没线索。
        var fileBytes = new FileInfo(filePath).Length;
        if (fileBytes == 0)
            throw new InvalidOperationException("文件是空的，没有可导入的内容。");
        if (fileBytes > Constants.MaxImportFileBytes)
        {
            throw new InvalidOperationException(
                $"文件过大（{fileBytes / 1024.0 / 1024.0:F1} MB），" +
                $"超过 {Constants.MaxImportFileBytes / 1024 / 1024} MB 的导入上限。" +
                "请先把文件拆分成多个再导入。");
        }

        var book = format switch
        {
            BookFormat.Epub => BuildFromExtracted(EpubTextExtractor.Extract(filePath), filePath, fileBytes, format),
            BookFormat.Pdf => BuildFromExtracted(PdfTextExtractor.Extract(filePath), filePath, fileBytes, format),
            _ => BuildFromTxt(filePath, fileBytes),
        };
        Persist(book);

        Log.Information("导入完成 {Title} 格式={Format} 卷数={Volumes} 章数={Chapters}",
            book.Title, book.SourceFormat, book.TotalVolumes, book.TotalChapters);

        return book;
    }

    /// <summary>TXT：一次性读入字节 → 检测编码 → 直接在字节流上扫行解析卷章。</summary>
    private Book BuildFromTxt(string filePath, long fileBytes)
    {
        // 一次性读入字节（只读一遍，避免先采样检测再全文解码的双重 IO）
        var bytes = File.ReadAllBytes(filePath);

        // 编码检测（容错：坏字节替换为 U+FFFD，不会导入失败）
        var encoding = _detector.Detect(bytes);
        Log.Debug("检测到编码 {Encoding} ({WebName})", encoding.EncodingName, encoding.WebName);

        // 卷章解析：直接在字节流上扫行，偏移精确，
        // 不受容错解码（U+FFFD 替换）导致的重编码长度漂移影响
        var bomLength = _detector.GetPreambleLength(filePath, encoding);
        var book = _parser.Parse(bytes, filePath, encoding, bomLength);
        book.Encoding = encoding.WebName ?? encoding.EncodingName;
        book.SourceFormat = BookFormatDetector.ToStorageValue(BookFormat.Txt);
        return book;
    }

    /// <summary>EPUB / PDF：把解析结果落成正文缓存，章节偏移指向缓存文件。</summary>
    private Book BuildFromExtracted(
        ExtractedContent content, string filePath, long fileBytes, BookFormat format)
    {
        return ExtractedTextCache.Write(content, filePath, fileBytes, format, _options.CacheDirectory);
    }

    /// <summary>入库：Books + Volumes/Chapters + 初始阅读进度。</summary>
    private void Persist(Book book)
    {
        var bookId = _db.InsertBook(book);
        book.Id = bookId;

        // 把内存中的 Volume/Chapter 重新写库（会回填 Id）
        _db.InsertVolumes(bookId, book.Volumes);

        // 更新总数
        _db.UpdateBookTotals(bookId, book.TotalChapters, book.TotalVolumes);

        // 初始化阅读进度
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
    }
}
