using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 把 EPUB / PDF 解析出的正文写成一份 UTF-8 纯文本缓存，并直接算出每章的字节偏移。
///
/// 为什么要落盘：整套阅读链路（章节偏移 → 按偏移回读 → 分页 / 书签 / 朗读）都建立在
/// 「源文件是一份可随机读取的文本」这个前提上。EPUB 是 zip、PDF 是分页二进制，
/// 都无法按偏移回读，因此导入时转成纯文本缓存，阅读时走的是与 TXT 完全相同的代码路径。
/// </summary>
public static class ExtractedTextCache
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>缓存文件路径：源文件绝对路径的 SHA-256 前 32 位（同一本书重复导入会覆盖同一份缓存）。</summary>
    public static string GetCachePath(string sourcePath, string? cacheDir = null)
    {
        var dir = string.IsNullOrWhiteSpace(cacheDir) ? Constants.ImportCacheDir : cacheDir!;
        var full = Path.GetFullPath(sourcePath).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full)));
        return Path.Combine(dir, hash[..32] + ".txt");
    }

    /// <summary>
    /// 写出缓存并构造 <see cref="Book"/>（章节偏移指向缓存文件）。
    /// </summary>
    /// <param name="content">解析结果</param>
    /// <param name="sourcePath">原始 EPUB/PDF 路径（作为书架唯一键）</param>
    /// <param name="sourceFileSize">原始文件大小</param>
    /// <param name="format">来源格式</param>
    /// <param name="cacheDir">缓存目录（测试可注入临时目录）</param>
    public static Book Write(
        ExtractedContent content,
        string sourcePath,
        long sourceFileSize,
        BookFormat format,
        string? cacheDir = null)
    {
        var cachePath = GetCachePath(sourcePath, cacheDir);
        var dir = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var volume = new Volume { VolumeNumber = 0, Title = "正文", BookId = 0 };
        var tempPath = cachePath + ".tmp";

        long offset = 0;
        int lineNumber = 0;
        int chapterIndex = 0;

        try
        {
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (var section in content.Sections)
                {
                    var title = string.IsNullOrWhiteSpace(section.Title)
                        ? $"第 {chapterIndex + 1} 章"
                        : section.Title.Trim();
                    var body = NormalizeBody(section.Text);

                    var text = new StringBuilder(title.Length + body.Length + 2);
                    text.Append(title).Append('\n');
                    if (body.Length > 0) text.Append(body).Append('\n');

                    var bytes = Utf8NoBom.GetBytes(text.ToString());
                    if (offset + bytes.Length > Constants.MaxExtractedTextBytes)
                    {
                        throw new InvalidOperationException(
                            $"解析出的正文超过 {Constants.MaxExtractedTextBytes / 1024 / 1024} MB 上限，已中止导入。" +
                            "这个文件可能不是正常的电子书。");
                    }

                    var chapter = new Chapter
                    {
                        ChapterNumber = chapterIndex,
                        DisplayNumber = chapterIndex + 1,
                        Title = title,
                        StartPosition = offset,
                        EndPosition = offset + bytes.Length,
                        StartLineNumber = lineNumber,
                        LineCount = CountLines(text),
                    };

                    fs.Write(bytes, 0, bytes.Length);
                    offset += bytes.Length;
                    lineNumber += chapter.LineCount;
                    chapterIndex++;

                    volume.Chapters.Add(chapter);
                }
            }

            if (volume.Chapters.Count == 0)
                throw new InvalidOperationException("解析结果里没有可阅读的章节。");

            // 原子替换：写一半崩了也不会留下半份缓存
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }

        volume.StartPosition = 0;
        volume.EndPosition = offset;

        var title2 = string.IsNullOrWhiteSpace(content.Title)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : content.Title.Trim();

        var book = new Book
        {
            Title = title2,
            Author = string.IsNullOrWhiteSpace(content.Author) ? null : content.Author!.Trim(),
            FilePath = sourcePath,
            ContentPath = cachePath,
            FileSize = sourceFileSize,
            Encoding = "utf-8",
            SourceFormat = BookFormatDetector.ToStorageValue(format),
            TotalVolumes = 1,
            TotalChapters = volume.Chapters.Count,
            Volumes = new List<Volume> { volume },
        };

        Log.Information("电子书正文缓存已生成 {Cache}（{Chapters} 章 / {Bytes} 字节）",
            cachePath, volume.Chapters.Count, offset);
        return book;
    }

    /// <summary>
    /// 删除缓存文件。只允许删缓存目录里的文件：ContentPath 来自数据库，
    /// 万一被改坏成别的路径，也不能让「移除书籍」变成删用户文件的入口。
    /// </summary>
    public static bool TryDeleteCache(string? contentPath, string? cacheDir = null)
    {
        if (string.IsNullOrWhiteSpace(contentPath)) return false;

        var dir = Path.GetFullPath(string.IsNullOrWhiteSpace(cacheDir) ? Constants.ImportCacheDir : cacheDir!);
        string full;
        try
        {
            full = Path.GetFullPath(contentPath!);
        }
        catch
        {
            return false;
        }

        var prefix = dir.EndsWith(Path.DirectorySeparatorChar) ? dir : dir + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("拒绝删除缓存目录之外的文件: {Path}", full);
            return false;
        }

        return TryDeleteFile(full);
    }

    /// <summary>正文统一换行、去掉首尾空白。</summary>
    internal static string NormalizeBody(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Trim('\n', ' ', '\t');
    }

    private static int CountLines(StringBuilder sb)
    {
        int count = 0;
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '\n') count++;
        }
        return count;
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "删除文件失败: {Path}", path);
        }
        return false;
    }
}
