using System;
using System.IO;

namespace FloatingNovelReader.Models;

/// <summary>支持的书籍来源格式。</summary>
public enum BookFormat
{
    /// <summary>纯文本（沿用原有的字节偏移解析链路）。</summary>
    Txt,

    /// <summary>EPUB 电子书（zip + XHTML，导入时解析成纯文本）。</summary>
    Epub,

    /// <summary>PDF 文档（导入时抽取文字层）。</summary>
    Pdf,
}

/// <summary>
/// 格式识别：先看扩展名，扩展名不认识（或被改错）时再看文件头魔数。
/// 魔数只做「像不像」，真正的合法性校验交给各自的解析器（错误信息更具体）。
/// </summary>
public static class BookFormatDetector
{
    /// <summary>文件选择对话框的过滤器串（顺序即下拉顺序）。</summary>
    public const string OpenFileFilter =
        "电子书 (*.txt;*.epub;*.pdf)|*.txt;*.epub;*.pdf|" +
        "TXT 文本 (*.txt)|*.txt|" +
        "EPUB 电子书 (*.epub)|*.epub|" +
        "PDF 文档 (*.pdf)|*.pdf|" +
        "所有文件 (*.*)|*.*";

    /// <summary>拖拽导入时接受的扩展名。</summary>
    public static readonly string[] SupportedExtensions = { ".txt", ".epub", ".pdf" };

    /// <summary>扩展名是否属于支持的格式。</summary>
    public static bool HasSupportedExtension(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        foreach (var e in SupportedExtensions)
        {
            if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>识别格式；无法识别返回 null（调用方给出「仅支持 TXT/EPUB/PDF」提示）。</summary>
    public static BookFormat? Detect(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        switch (ext)
        {
            case ".epub": return BookFormat.Epub;
            case ".pdf": return BookFormat.Pdf;
            case ".txt":
            case ".text":
            case ".log":
                return BookFormat.Txt;
        }

        return DetectByMagic(filePath);
    }

    /// <summary>按文件头魔数识别（读前 8 字节，失败返回 null）。</summary>
    public static BookFormat? DetectByMagic(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var head = new byte[8];
            int n = fs.Read(head, 0, head.Length);

            // %PDF-
            if (n >= 5 && head[0] == (byte)'%' && head[1] == (byte)'P' &&
                head[2] == (byte)'D' && head[3] == (byte)'F' && head[4] == (byte)'-')
                return BookFormat.Pdf;

            // PK\x03\x04（普通 zip）/ PK\x05\x06（空 zip）：EPUB 是 zip，进一步校验在解析器里
            if (n >= 4 && head[0] == 0x50 && head[1] == 0x4B &&
                ((head[2] == 0x03 && head[3] == 0x04) || (head[2] == 0x05 && head[3] == 0x06)))
                return BookFormat.Epub;
        }
        catch
        {
            // 读不到文件头就当作无法识别
        }

        return null;
    }

    /// <summary>存库用的稳定标识（小写）。</summary>
    public static string ToStorageValue(BookFormat format) => format switch
    {
        BookFormat.Epub => "epub",
        BookFormat.Pdf => "pdf",
        _ => "txt",
    };

    /// <summary>从库里的字符串还原格式；空值/未知值按 TXT 处理（老库没有这一列）。</summary>
    public static BookFormat FromStorageValue(string? value) => value?.ToLowerInvariant() switch
    {
        "epub" => BookFormat.Epub,
        "pdf" => BookFormat.Pdf,
        _ => BookFormat.Txt,
    };

    /// <summary>界面上显示的格式名。</summary>
    public static string ToDisplayName(BookFormat format) => format switch
    {
        BookFormat.Epub => "EPUB",
        BookFormat.Pdf => "PDF",
        _ => "TXT",
    };
}
