using System;
using System.IO;
using System.Text;
using Ude;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 文本编码自动检测。
/// 优先顺序：BOM -> Ude 启发式检测 -> 退回 UTF-8。
/// </summary>
public sealed class TextEncoderDetector
{
    /// <summary>
    /// 检测字节流的编码。
    /// </summary>
    public Encoding Detect(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return Encoding.UTF8;

        // 1. BOM 检测
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;          // UTF-16 LE
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode; // UTF-16 BE
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return new UTF32Encoding(true, true);
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            return new UTF32Encoding(false, true);

        // 2. Ude 启发式检测
        try
        {
            var detector = new CharsetDetector();
            detector.Feed(bytes, 0, bytes.Length);
            detector.DataEnd();
            if (detector.Charset != null)
            {
                var enc = Encoding.GetEncoding(detector.Charset, new EncoderExceptionFallback(), new DecoderExceptionFallback());
                return enc;
            }
        }
        catch
        {
            // 忽略检测异常，走兜底
        }

        // 3. 兜底 UTF-8
        return new UTF8Encoding(false, true);
    }

    /// <summary>从文件检测编码（只读前 64KB 提速）</summary>
    public Encoding DetectFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return Encoding.UTF8;

        var sampleSize = (int)Math.Min(64 * 1024, new FileInfo(filePath).Length);
        var buffer = new byte[sampleSize];
        using (var fs = File.OpenRead(filePath))
            fs.Read(buffer, 0, buffer.Length);

        return Detect(buffer);
    }

    /// <summary>用检测到的编码安全解码整个文件（容错）</summary>
    public string DecodeFile(string filePath, Encoding encoding)
    {
        using var fs = File.OpenRead(filePath);
        using var sr = new StreamReader(fs, encoding, true);
        return sr.ReadToEnd();
    }
}
