using System.IO;
using System.Text;
using FloatingNovelReader.Models;
using FloatingNovelReader.Core;
using Serilog;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 按章节的字节偏移（StartPosition / EndPosition）从源文件读取章节内容。
/// 与 ChapterParser 计算偏移的规则配对，是阅读窗口取正文的唯一入口。
/// </summary>
public static class ChapterContentReader
{
    static ChapterContentReader() => EncodingSupport.EnsureRegistered();

    public static string Read(string filePath, Chapter chapter, string? encodingName)
    {
        var start = chapter.StartPosition;
        var end = chapter.EndPosition;

        // 偏移来自数据库。被外部工具改坏、或导入中断时可能是负数/倒置，
        // 而 `(int)(end - start)` 得到负数后 new byte[len] 会抛 OverflowException，
        // 报错信息对用户完全没有线索 —— 这里早返回空内容并记日志。
        if (start < 0 || end < start)
        {
            Log.Warning("章节偏移非法 (Start={Start}, End={End})，返回空内容: {Title}",
                start, end, chapter.Title);
            return string.Empty;
        }

        using var fs = File.OpenRead(filePath);
        fs.Seek(start, SeekOrigin.Begin);

        // 夹到 int 范围，避免 >2GB 的偏移差在强转时溢出
        var rawLen = end - start;
        int len = rawLen > int.MaxValue ? int.MaxValue : (int)rawLen;
        if (len == 0) return string.Empty;

        var buf = new byte[len];
        int total = 0;
        while (total < len)
        {
            int n = fs.Read(buf, total, len - total);
            if (n == 0) break;
            total += n;
        }

        Encoding enc;
        try
        {
            // 替换回退：坏字节显示为 U+FFFD 而不是抛异常导致整章加载失败
            enc = Encoding.GetEncoding(encodingName ?? "utf-8");
        }
        catch
        {
            enc = Encoding.UTF8;
        }

        try
        {
            return enc.GetString(buf, 0, total);
        }
        catch
        {
            // 兜底用 UTF-8
            return Encoding.UTF8.GetString(buf, 0, total);
        }
    }
}
