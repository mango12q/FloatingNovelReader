using System;
using System.Collections.Generic;
using System.Text;

namespace FloatingNovelReader.Services;

/// <summary>
/// 朗读文本的清洗、转义与切分。
///
/// 全部是纯函数，与网络/播放无关，因此可以逐条单测。
/// 规则来自 edge-tts-plan.md §4.6（已按参考实现核对）：
///   1. 先做**字符清洗**：码点 0–8、11–12、14–31 必须替换成空格，
///      否则服务端会直接报错（竖排制表符 0x0B 在 OCR 出来的 TXT 里很常见）；
///   2. 再做 **XML 转义**，然后嵌入 SSML；
///   3. 按 **4096 UTF-8 字节**切分（不是 200 字，原稿错了一个数量级），
///      且不能切断 UTF-8 多字节序列，也不能切断 XML 实体（如 &amp;）。
/// </summary>
public static class TtsSegmenter
{
    /// <summary>句末标点：优先在这些位置断开。</summary>
    private static readonly char[] SentenceEnders = { '。', '！', '？', '!', '?', '…', '；', ';', '\n' };

    /// <summary>次级断点：句末标点找不到时用这些。</summary>
    private static readonly char[] SoftBreaks = { '，', '、', '：', ',', ':', ' ', '\t', '）', '》', '」' };

    /// <summary>把服务端不接受的码点替换成空格。</summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var code = (int)ch;
            if ((code >= 0 && code <= 8) || (code >= 11 && code <= 12) || (code >= 14 && code <= 31))
                sb.Append(' ');
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>XML 转义（SSML 里文本必须转义）。</summary>
    public static string EscapeXml(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>清洗 + 转义：得到可以直接嵌入 SSML 的文本。</summary>
    public static string Prepare(string? text) => EscapeXml(Sanitize(text));

    /// <summary>协议允许的单次请求上限（UTF-8 字节）。</summary>
    public const int MaxRequestBytes = TtsProtocol.MaxRequestBytes;

    /// <summary>
    /// 实际切片的**目标**大小——刻意远小于协议上限。
    ///
    /// 4096 字节的中文约 1365 字，合成出来是 **4~5 分钟**音频、约 1.7MB mp3、
    /// 解码后约 13MB PCM。用它当切片大小会有两个后果：
    ///   1. 必须等整段合成 + 解码完才出第一声（首播延迟以分钟计）；
    ///   2. 整段 PCM 必须一次性装进播放缓冲（NAudio 默认缓冲只有 5 秒 → "Buffer full"）。
    /// 取 ~600 字节（约 200 汉字 ≈ 30 秒）在首播延迟与请求数之间取得平衡。
    /// </summary>
    public const int PreferredSegmentBytes = 600;

    /// <summary>把整章文本切成可直接送进 SSML 的片段（每段已清洗 + 转义）。</summary>
    public static IReadOnlyList<string> SplitChapter(string? chapterText) =>
        SplitByBytes(Prepare(chapterText), PreferredSegmentBytes);

    /// <summary>
    /// 按 UTF-8 字节上限切分（输入应为已转义的文本）。
    /// 优先在句末标点断，其次在逗号/空格断，最后才硬断；
    /// 任何情况下都不切断 UTF-8 多字节序列或 XML 实体。
    /// </summary>
    public static IReadOnlyList<string> SplitByBytes(string? text, int maxBytes)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;
        if (maxBytes <= 0) maxBytes = TtsProtocol.MaxRequestBytes;

        var start = 0;
        while (start < text.Length)
        {
            // 剩下的一次装得下就收尾
            if (Encoding.UTF8.GetByteCount(text.AsSpan(start)) <= maxBytes)
            {
                var tail = text[start..].Trim();
                if (tail.Length > 0) result.Add(tail);
                break;
            }

            var bytes = 0;
            var i = start;
            var atSentence = -1;
            var atSoft = -1;

            while (i < text.Length)
            {
                var charLen = IsHighSurrogatePair(text, i) ? 2 : 1;
                var add = Encoding.UTF8.GetByteCount(text.AsSpan(i, charLen));
                if (bytes + add > maxBytes) break;

                bytes += add;
                i += charLen;

                if (i >= text.Length) continue;
                var prev = text[i - 1];
                if (Array.IndexOf(SentenceEnders, prev) >= 0) atSentence = i;
                else if (Array.IndexOf(SoftBreaks, prev) >= 0) atSoft = i;
            }

            var splitAt = atSentence > start ? atSentence
                        : atSoft > start ? atSoft
                        : i;

            splitAt = AvoidSplittingEntity(text, start, splitAt);
            if (splitAt <= start)
            {
                // 单个字符就超过上限（例如上限 2 字节遇到 3 字节的汉字）。
                // 不能返回空、也不能死循环：只能硬取一个"完整字符"（代理对整体取）。
                splitAt = start + (IsHighSurrogatePair(text, start) ? 2 : 1);
            }
            if (splitAt > text.Length) splitAt = text.Length;

            var chunk = text[start..splitAt].Trim();
            if (chunk.Length > 0) result.Add(chunk);
            start = splitAt;
        }

        return result;
    }

    /// <summary>若切点落在未闭合的 XML 实体中间，则退回到 '&amp;' 处。</summary>
    private static int AvoidSplittingEntity(string text, int start, int splitAt)
    {
        if (splitAt <= start) return splitAt;

        var amp = text.LastIndexOf('&', splitAt - 1);
        if (amp < start) return splitAt;

        var semi = text.IndexOf(';', amp);
        return semi >= 0 && semi < splitAt ? splitAt : amp;
    }

    private static bool IsHighSurrogatePair(string s, int i) =>
        char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]);
}
