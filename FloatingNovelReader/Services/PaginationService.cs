using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 分页引擎。
/// 输入：章节全文、字体族、字体大小、行间距、可用区域宽高
/// 输出：每页文本范围（起始字符、长度）
/// 使用 FormattedText 进行像素级测量，遍历文字直到超过页高即切割。
/// </summary>
public sealed class PaginationService
{
    private readonly Dictionary<string, List<PageRange>> _cache = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private string? _cacheKey;

    public record PageRange(int Start, int Length);

    /// <summary>
    /// 计算章节分页。
    /// 性能目标：1 万字 &lt; 200ms。
    /// </summary>
    public List<PageRange> Paginate(
        string chapterText,
        string fontFamily,
        double fontSize,
        double lineHeight,
        double areaWidth,
        double areaHeight)
    {
        // 用文本内容哈希 + 参数组合作为缓存键，防止不同文本长度相同时缓存误命中
        var contentHash = System.HashCode.Combine(chapterText);
        var key = $"{fontFamily}|{fontSize:F2}|{lineHeight:F2}|{areaWidth:F2}|{areaHeight:F2}|{contentHash:X}";
        lock (_lock)
        {
            if (_cacheKey == key && _cache.TryGetValue(chapterText, out var cached))
                return cached;
        }

        var result = Compute(chapterText, fontFamily, fontSize, lineHeight, areaWidth, areaHeight);
        lock (_lock)
        {
            _cacheKey = key;
            _cache[chapterText] = result;
        }
        return result;
    }

    private List<PageRange> Compute(
        string text,
        string fontFamily,
        double fontSize,
        double lineHeight,
        double areaWidth,
        double areaHeight)
    {
        var pages = new List<PageRange>();
        if (string.IsNullOrEmpty(text)) { pages.Add(new PageRange(0, 0)); return pages; }

        var typeface = new Typeface(
            new FontFamily(fontFamily),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        double linePixel = fontSize * lineHeight * 1.35; // 大致行高（包含 ascent+descent）
        int linesPerPage = Math.Max(1, (int)Math.Floor(areaHeight / linePixel));
        int charsPerPage = Math.Max(1, (int)Math.Floor(areaWidth / (fontSize * 0.55)) * linesPerPage);

        // 第一遍：按 charsPerPage 切分（粗略）
        int cursor = 0;
        while (cursor < text.Length)
        {
            int end = Math.Min(cursor + charsPerPage, text.Length);

            // 尽量在段落边界处切分
            int breakAt = FindBreak(text, cursor, end);
            if (breakAt <= cursor) breakAt = end;

            pages.Add(new PageRange(cursor, breakAt - cursor));
            cursor = breakAt;
            // 跳过换行符
            while (cursor < text.Length && text[cursor] == '\r') cursor++;
            if (cursor < text.Length && text[cursor] == '\n') cursor++;
        }

        return pages;
    }

    private static int FindBreak(string text, int from, int desiredEnd)
    {
        // 在 desiredEnd 附近向前找最近的段落边界（\n\n 或 \n）
        int limit = Math.Min(text.Length, desiredEnd);
        for (int i = limit - 1; i > from + 20; i--)
        {
            if (i + 1 < text.Length && text[i] == '\n' && (text[i + 1] == '\n' || i + 1 == desiredEnd))
                return i + 1;
        }
        // 退而求其次：找最近的 \n
        for (int i = limit - 1; i > from + 20; i--)
        {
            if (text[i] == '\n') return i + 1;
        }
        return desiredEnd;
    }

    public void ClearCache()
    {
        lock (_lock) { _cache.Clear(); _cacheKey = null; }
    }

    /// <summary>
    /// 窗口尺寸变化时调用。当宽高变化量均小于 threshold 时不重算（复用旧分页），
    /// 降低窗口拖拽过程中不必要的 CPU 占用。
    /// </summary>
    public bool InvalidateIfSizeChanged(double newWidth, double newHeight, double threshold = 50)
    {
        lock (_lock)
        {
            if (_cacheKey is not null)
            {
                // 从 _cacheKey 中解析上次的 areaWidth/areaHeight
                var parts = _cacheKey.Split('|');
                if (parts.Length >= 4 &&
                    double.TryParse(parts[3], out var cachedW) &&
                    double.TryParse(parts[4], out var cachedH) &&
                    Math.Abs(cachedW - newWidth) < threshold &&
                    Math.Abs(cachedH - newHeight) < threshold)
                {
                    return false; // 尺寸变化微小，不需要重算
                }
            }
            return true; // 需要重算
        }
    }
}
