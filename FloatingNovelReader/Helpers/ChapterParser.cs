using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 卷章正则解析引擎。
/// 详见 4.2 节规格说明。匹配优先级：
///   1. 卷标题
///   2. 章节标题（中英文 + 阿拉伯数字 + 数字编号）
/// 数字归一化：「第一章」「第1章」「第001章」「Chapter 1」都映射到序号 1。
/// </summary>
public sealed class ChapterParser
{
    // 中文章节：第[一二三四五六七八九十百千万零〇两]+章 / 节 / 回 / 话
    // 「回(?!合)」防止正文行首的「第三回合」被误切成一章
    private static readonly Regex ReChineseChapter = new(
        @"^\s*第([零〇一二三四五六七八九十百千万两]+|\d+)\s*(章|节|回(?!合)|话)\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 中文卷：第[一..]卷 / 部；「部(?!分)」防止「第一部分…」被误判成新卷
    private static readonly Regex ReChineseVolume = new(
        @"^\s*第([零〇一二三四五六七八九十百千万两]+|\d+)\s*(?:卷|部(?!分))\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 特殊章节标题：楔子 / 序章 / 番外 / 尾声 / 后记 等（独立成行，可带序号或副标题）
    private static readonly Regex ReSpecialChapter = new(
        @"^\s*(序章|序言|自序|引子|楔子|间章|番外|外传|尾声|终章|后记|大结局)(.*)$",
        RegexOptions.Compiled);

    // 英文 Chapter / CHAPTER（带或不带数字）
    private static readonly Regex ReEnglishChapter = new(
        @"^\s*[Cc][Hh][Aa][Pp][Tt][Ee][Rr]\s*([0-9零〇一二三四五六七八九十百千万两]+)\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 英文 Volume / VOLUME
    private static readonly Regex ReEnglishVolume = new(
        @"^\s*[Vv][Oo][Ll][Uu][Mm][Ee]\s*([0-9零〇一二三四五六七八九十百千万两]+)\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 中文：卷一 / 卷1
    private static readonly Regex RePlainVolume = new(
        @"^\s*卷\s*([零〇一二三四五六七八九十百千万两]+|\d+)\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 纯数字编号：1、xxx  或 1. xxx
    private static readonly Regex ReNumbered = new(
        @"^\s*(\d+)\s*[、\.．]\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 作者
    private static readonly Regex ReAuthor = new(
        @"^\s*(?:作者|Author)\s*[：:]\s*(.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // 中文数字 -> 阿拉伯数字（支持 0~9999 内整数）
    private static readonly Dictionary<char, int> CnDigitMap = new()
    {
        ['零'] = 0, ['〇'] = 0,
        ['一'] = 1, ['二'] = 2, ['三'] = 3, ['四'] = 4, ['五'] = 5,
        ['六'] = 6, ['七'] = 7, ['八'] = 8, ['九'] = 9,
        ['两'] = 2,
    };
    private static readonly Dictionary<char, int> CnUnitMap = new()
    {
        ['十'] = 10, ['百'] = 100, ['千'] = 1000, ['万'] = 10000,
    };

    public static int ParseChineseNumber(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        if (int.TryParse(s, out var n)) return n;

        // 简单累加算法：个位数 + 单位乘 + 累加
        int result = 0;
        int section = 0;   // 当前段（万 段内）
        int last = 0;
        foreach (var c in s)
        {
            if (CnDigitMap.TryGetValue(c, out var d))
            {
                last = d;
            }
            else if (CnUnitMap.TryGetValue(c, out var u))
            {
                if (u == 10000)
                {
                    section = (section + last) * 10000;
                    result += section;
                    section = 0;
                    last = 0;
                }
                else if (u == 1000 || u == 100 || u == 10)
                {
                    if (last == 0 && u == 10 && result == 0 && section == 0)
                    {
                        // "十" 在开头，等价于 1*10
                        section += 1 * u;
                    }
                    else
                    {
                        section += last * u;
                    }
                    last = 0;
                }
            }
        }
        result += section + last;
        return result;
    }

    /// <summary>
    /// 解析一整本 TXT，返回 Book（含 Volumes / Chapters）。
    /// 注意：text 已经是解码后的字符串；positions 数组（可选）记录每行在原字节流中的位置。
    /// </summary>
    /// <param name="text">全文内容（已解码）</param>
    /// <param name="filePath">原文件路径</param>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <param name="encoding">文件实际编码，用于正确计算字节偏移量</param>
    /// <param name="byteOffset">正文起始的字节偏移（文件头 BOM 的字节数，解码后的 text 中已被剥掉）</param>
    public Book Parse(string text, string filePath, long fileSize, Encoding? encoding = null, long byteOffset = 0)
    {
        var book = new Book
        {
            FilePath = filePath,
            FileSize = fileSize,
            Title = System.IO.Path.GetFileNameWithoutExtension(filePath),
        };

        // 尝试提取作者
        var authorMatch = ReAuthor.Match(text);
        if (authorMatch.Success)
        {
            book.Author = authorMatch.Groups[1].Value.Trim();
        }

        // 拆分行为 line list。
        // 偏移按「实际编码」计算：换行符在 UTF-16 下是 2 字节，硬编码 +1 会让
        // 每行偏移累积错位，阅读时按偏移回读的章节内容全是乱码。
        var lines = text.Split('\n');
        var linePositions = new long[lines.Length + 1];
        var byteEnc = encoding ?? Encoding.UTF8;
        int newlineBytes = byteEnc.GetByteCount("\n");
        long pos = byteOffset;
        for (int i = 0; i < lines.Length; i++)
        {
            linePositions[i] = pos;
            pos += byteEnc.GetByteCount(lines[i]);
            if (i < lines.Length - 1) pos += newlineBytes; // 行间必有 \n；最后一行后不一定有
        }
        linePositions[lines.Length] = pos;

        // 当前卷 / 当前章
        var volumes = new List<Volume>();
        var currentVolume = new Volume
        {
            VolumeNumber = 0,
            Title = "正文",
            BookId = 0,
        };
        volumes.Add(currentVolume);

        Volume? prologueVolume = null; // 序章（首个卷/章之前的部分）
        int volumeNumberCounter = 0;
        int chapterNumberCounter = 0;
        var preVolumeContentLines = new List<int>(); // 第一个卷/章标题之前的行
        int firstHeaderLineIdx = -1;

        // 找到首个 header
        for (int i = 0; i < lines.Length; i++)
        {
            if (TryMatchHeader(lines[i], out _, out _, out _, out _))
            {
                firstHeaderLineIdx = i;
                break;
            }
        }

        if (firstHeaderLineIdx < 0)
        {
            // 整个文件没有章节标题，作为单章
            var singleChapter = new Chapter
            {
                ChapterNumber = 0,
                DisplayNumber = 1,
                Title = book.Title,
                StartPosition = byteOffset,
                EndPosition = fileSize,
                StartLineNumber = 0,
                LineCount = lines.Length,
            };
            currentVolume.Chapters.Add(singleChapter);
            currentVolume.StartPosition = byteOffset;
            currentVolume.EndPosition = fileSize;
            book.Volumes = volumes;
            book.TotalVolumes = 1;
            book.TotalChapters = 1;
            return book;
        }

        // 序章/引言
        if (firstHeaderLineIdx > 0)
        {
            var preTitle = (book.Author is { Length: > 0 })
                ? $"引言（{book.Author}）"
                : "引言";
            var preChapter = new Chapter
            {
                ChapterNumber = chapterNumberCounter++,
                DisplayNumber = 0,
                Title = preTitle,
                StartPosition = byteOffset,
                EndPosition = linePositions[firstHeaderLineIdx],
                StartLineNumber = 0,
                LineCount = firstHeaderLineIdx,
            };
            // 第一个卷标题之前的序章归属于一个特殊「正文」卷
            currentVolume.Title = "正文";
            currentVolume.StartPosition = byteOffset;
            currentVolume.Chapters.Add(preChapter);
        }

        Chapter? currentChapter = null;

        for (int i = firstHeaderLineIdx; i < lines.Length; i++)
        {
            var line = lines[i];
            if (TryMatchHeader(line, out var kind, out var numStr, out var tail, out var isVolume))
            {
                // 结束上一章
                if (currentChapter != null)
                {
                    currentChapter.EndPosition = linePositions[i];
                    currentChapter.LineCount = (i - currentChapter.StartLineNumber);
                }

                if (isVolume)
                {
                    // 新卷
                    volumeNumberCounter++;
                    currentVolume = new Volume
                    {
                        VolumeNumber = volumeNumberCounter,
                        Title = line.Trim(),
                        BookId = 0,
                        StartPosition = linePositions[i],
                    };
                    volumes.Add(currentVolume);
                    // 重置章节计数（每卷独立编号）
                    chapterNumberCounter = 0;

                    // 跳过紧跟标题后的空行
                    // 暂不创建 Chapter，等待第一个非空行/下一个章节标题
                    currentChapter = null;
                }
                else
                {
                    // 新章节: 直接把整行作为标题 (用户期望: "第3章 斗破苍穹")
                    var number = ParseChineseNumber(numStr);
                    if (number <= 0) number = chapterNumberCounter + 1;

                    currentChapter = new Chapter
                    {
                        ChapterNumber = chapterNumberCounter++,
                        DisplayNumber = number,
                        Title = line.Trim(),
                        StartPosition = linePositions[i],
                        StartLineNumber = i,
                    };
                    currentVolume.Chapters.Add(currentChapter);
                }
            }
        }

        // 结束最后一章
        if (currentChapter != null)
        {
            currentChapter.EndPosition = fileSize;
            currentChapter.LineCount = lines.Length - currentChapter.StartLineNumber;
            if (currentVolume != null)
                currentVolume.EndPosition = fileSize;
        }

        // 把每卷的范围补齐
        for (int v = 0; v < volumes.Count; v++)
        {
            var vol = volumes[v];
            if (vol.Chapters.Count > 0)
            {
                if (vol.StartPosition == 0 && v > 0)
                    vol.StartPosition = vol.Chapters[0].StartPosition;
                vol.EndPosition = vol.Chapters[^1].EndPosition;
            }
        }

        book.Volumes = volumes;
        book.TotalVolumes = volumes.Count;
        book.TotalChapters = book.FlatChapters().Count();
        return book;
    }

    /// <summary>
    /// 尝试匹配一行是否为一个卷/章标题。返回 (kind, number, tail, isVolume)。
    /// </summary>
    private bool TryMatchHeader(string line, out string kind, out string number, out string? tail, out bool isVolume)
    {
        kind = ""; number = ""; tail = null; isVolume = false;
        if (string.IsNullOrWhiteSpace(line)) return false;

        // 卷标题优先
        var m = ReChineseVolume.Match(line);
        if (m.Success) { isVolume = true; number = m.Groups[1].Value; tail = m.Groups[2].Value; kind = "CnVolume"; return true; }

        m = ReEnglishVolume.Match(line);
        if (m.Success) { isVolume = true; number = m.Groups[1].Value; tail = m.Groups[2].Value; kind = "EnVolume"; return true; }

        m = RePlainVolume.Match(line);
        if (m.Success) { isVolume = true; number = m.Groups[1].Value; tail = m.Groups[2].Value; kind = "PlainVolume"; return true; }

        // 章节
        m = ReChineseChapter.Match(line);
        if (m.Success) { number = m.Groups[1].Value; tail = m.Groups[3].Value; kind = "CnChapter"; return true; }

        m = ReEnglishChapter.Match(line);
        if (m.Success) { number = m.Groups[1].Value; tail = m.Groups[2].Value; kind = "EnChapter"; return true; }

        m = ReSpecialChapter.Match(line);
        if (m.Success && IsSpecialChapterHeader(line, m.Groups[2].Value))
        {
            number = ""; // 无编号，后续按顺序回填
            tail = m.Groups[2].Value.Trim();
            kind = "Special";
            return true;
        }

        m = ReNumbered.Match(line);
        if (m.Success)
        {
            // 简单编号"1、xxx"必须本身看起来像标题——粗略判：剩余部分长度 <= 60 且不包含过多标点
            var rest = m.Groups[2].Value.Trim();
            if (rest.Length <= 60 && rest.Length > 0)
            {
                number = m.Groups[1].Value;
                tail = rest;
                kind = "Numbered";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 特殊标题词（楔子/番外/尾声…）出现在行首时，还需像一个标题才算数：
    /// 词后必须是行尾、分隔符或序号（防止「楔子是一种…」这类正文行被误切），
    /// 且整行足够短。
    /// </summary>
    private static bool IsSpecialChapterHeader(string line, string rest)
    {
        if (line.Trim().Length > 40) return false;
        if (rest.Length == 0) return true;                       // 词独立成行，如「楔子」
        if (char.IsWhiteSpace(rest[0])) return true;             // 「尾声 黎明」

        var c = rest.TrimStart();
        if (c.Length == 0) return true;
        if ("：:、·—－-（(【[「《".IndexOf(c[0]) >= 0) return true;  // 「后记：感谢」
        if (char.IsDigit(c[0])) return true;                     // 「番外2」
        if (CnDigitMap.ContainsKey(c[0]) || CnUnitMap.ContainsKey(c[0])) return true; // 「番外一」
        return false;
    }
}
