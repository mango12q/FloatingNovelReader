using System.IO;
using System.Text;
using FloatingNovelReader.Models;

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
        using var fs = File.OpenRead(filePath);
        fs.Seek(chapter.StartPosition, SeekOrigin.Begin);
        int len = (int)(chapter.EndPosition - chapter.StartPosition);
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
            enc = Encoding.GetEncoding(encodingName ?? "utf-8",
                new EncoderExceptionFallback(), new DecoderExceptionFallback());
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
