using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 分页引擎。
/// 输入：章节全文、字体族、字体大小、行间距、可用区域宽高
/// 输出：每页文本范围（起始字符、长度）
/// 使用 WPF TextFormatter 逐行排版（与 TextBlock 渲染同一套布局引擎），
/// 行高 = FontSize × LineHeightFactor，与渲染侧 TextBlock.LineHeight 显式绑定值一致。
/// </summary>
public sealed class PaginationService
{
    private const int MaxCacheEntries = 8;

    private readonly Dictionary<string, List<PageRange>> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly object _lock = new();
    private double _lastAreaWidth;
    private double _lastAreaHeight;

    public record PageRange(int Start, int Length);

    /// <summary>
    /// 计算章节分页。
    /// areaWidth/areaHeight 应为「减去内边距后」的实际排版区域（DIP）。
    /// 性能目标：1 万字 &lt; 200ms。
    /// </summary>
    public List<PageRange> Paginate(
        string chapterText,
        string fontFamily,
        double fontSize,
        double lineHeight,
        double areaWidth,
        double areaHeight,
        FontWeight? fontWeight = null,
        FontStyle? fontStyle = null)
    {
        var weight = fontWeight ?? FontWeights.Normal;
        var style = fontStyle ?? FontStyles.Normal;
        var contentHash = chapterText.GetHashCode();
        var key = $"{fontFamily}|{fontSize:F2}|{lineHeight:F2}|{weight.ToOpenTypeWeight()}|{style}|{areaWidth:F2}|{areaHeight:F2}|{chapterText.Length}:{contentHash:X8}";

        lock (_lock)
        {
            _lastAreaWidth = areaWidth;
            _lastAreaHeight = areaHeight;
            if (_cache.TryGetValue(key, out var cached))
                return cached;
        }

        List<PageRange> result;
        try
        {
            result = Compute(chapterText, fontFamily, fontSize, lineHeight, areaWidth, areaHeight, weight, style);
        }
        catch (Exception ex)
        {
            // 排版引擎失败（如字体缺失）时退回粗略估算，保证阅读不中断
            Log.Warning(ex, "TextFormatter 分页失败，退回启发式估算");
            result = FallbackCompute(chapterText, fontSize, lineHeight, areaWidth, areaHeight);
        }

        lock (_lock)
        {
            _cache[key] = result;
            _cacheOrder.Enqueue(key);
            while (_cacheOrder.Count > MaxCacheEntries)
            {
                var oldest = _cacheOrder.Dequeue();
                _cache.Remove(oldest);
            }
        }
        return result;
    }

    private static List<PageRange> Compute(
        string text,
        string fontFamily,
        double fontSize,
        double lineHeightFactor,
        double areaWidth,
        double areaHeight,
        FontWeight weight,
        FontStyle style)
    {
        var pages = new List<PageRange>();
        if (string.IsNullOrEmpty(text)) { pages.Add(new PageRange(0, 0)); return pages; }

        double linePx = Math.Max(1, fontSize * lineHeightFactor);
        double wrapWidth = Math.Max(20, areaWidth);
        double pageHeight = Math.Max(linePx, areaHeight);

        var typeface = new Typeface(new FontFamily(fontFamily), style, weight, FontStretches.Normal);
        var runProps = new PageRunProperties(typeface, fontSize);
        var paraProps = new PageParagraphProperties(runProps, linePx);

        var formatter = TextFormatter.Create();
        var source = new StringTextSource(text, runProps);

        int cursor = 0;
        while (cursor < text.Length)
        {
            int pageStart = cursor;
            double used = 0;
            while (cursor < text.Length)
            {
                using var line = formatter.FormatLine(source, cursor, wrapWidth, paraProps, null);
                if (line == null || line.Length <= 0)
                {
                    cursor++; // 安全兜底，防死循环
                    continue;
                }
                // 本页已放不下更多行则切页（但每页至少放一行，避免极端行高卡死）
                if (used + line.Height > pageHeight + 0.01 && cursor > pageStart)
                    break;
                cursor += line.Length;
                // 末行可能连带消耗 TextEndOfParagraph 占位符，钳制到文本末尾
                if (cursor > text.Length) cursor = text.Length;
                used += line.Height;
            }
            pages.Add(new PageRange(pageStart, cursor - pageStart));
        }

        return pages;
    }

    private static List<PageRange> FallbackCompute(
        string text, double fontSize, double lineHeight, double areaWidth, double areaHeight)
    {
        var pages = new List<PageRange>();
        if (string.IsNullOrEmpty(text)) { pages.Add(new PageRange(0, 0)); return pages; }

        double linePixel = fontSize * lineHeight;
        int linesPerPage = Math.Max(1, (int)Math.Floor(areaHeight / linePixel));
        // CJK 字形宽约 1.0em；取 0.9 留一点余量
        int charsPerPage = Math.Max(1, (int)Math.Floor(areaWidth / (fontSize * 0.9)) * linesPerPage);

        int cursor = 0;
        while (cursor < text.Length)
        {
            int end = Math.Min(cursor + charsPerPage, text.Length);
            pages.Add(new PageRange(cursor, end - cursor));
            cursor = end;
        }
        return pages;
    }

    public void ClearCache()
    {
        lock (_lock) { _cache.Clear(); _cacheOrder.Clear(); }
    }

    /// <summary>
    /// 窗口尺寸变化时调用。当宽高变化量均小于 threshold 时不重算（复用旧分页），
    /// 降低窗口拖拽过程中不必要的 CPU 占用。
    /// </summary>
    public bool InvalidateIfSizeChanged(double newWidth, double newHeight, double threshold = 50)
    {
        lock (_lock)
        {
            if (Math.Abs(_lastAreaWidth - newWidth) < threshold &&
                Math.Abs(_lastAreaHeight - newHeight) < threshold)
            {
                return false; // 尺寸变化微小，不需要重算
            }
            return true; // 需要重算
        }
    }

    /// <summary>
    /// 排版属性：字号/字体/显式行高。与 TextBlock（LineHeight=LineHeightPixels, BlockLineHeight）一致。
    /// .NET Core WPF 中 GenericTextRunProperties 是 internal，需自行实现。
    /// </summary>
    private sealed class PageRunProperties : TextRunProperties
    {
        private static readonly CultureInfo ZhCn = CultureInfo.GetCultureInfo("zh-CN");
        private readonly Typeface _typeface;
        private readonly double _size;

        public PageRunProperties(Typeface typeface, double size)
        {
            _typeface = typeface;
            _size = size;
        }

        public override Brush? BackgroundBrush => null;
        public override BaselineAlignment BaselineAlignment => BaselineAlignment.Baseline;
        public override CultureInfo? CultureInfo => ZhCn;
        public override double FontHintingEmSize => _size;
        public override double FontRenderingEmSize => _size;
        public override Brush ForegroundBrush => Brushes.Black;
        public override NumberSubstitution? NumberSubstitution => null;
        public override TextDecorationCollection? TextDecorations => null;
        public override TextEffectCollection? TextEffects => null;
        public override Typeface Typeface => _typeface;
        public override TextRunTypographyProperties? TypographyProperties => null;
    }

    private sealed class PageParagraphProperties : TextParagraphProperties
    {
        private readonly TextRunProperties _runProps;
        private readonly double _lineHeight;

        public PageParagraphProperties(TextRunProperties runProps, double lineHeight)
        {
            _runProps = runProps;
            _lineHeight = lineHeight;
        }

        public override TextRunProperties DefaultTextRunProperties => _runProps;
        public override bool FirstLineInParagraph => true;
        public override FlowDirection FlowDirection => FlowDirection.LeftToRight;
        public override double Indent => 0;
        public override double LineHeight => _lineHeight;
        public override double DefaultIncrementalTab => 0;
        public override TextAlignment TextAlignment => TextAlignment.Left;
        public override TextMarkerProperties? TextMarkerProperties => null;
        public override TextWrapping TextWrapping => TextWrapping.Wrap;
    }

    /// <summary>
    /// 把章节全文按换行符提供给 TextFormatter 的字符源。
    /// \r\n 视为一个换行；TextEndOfLine 消耗换行符本身。
    /// </summary>
    private sealed class StringTextSource : TextSource
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("zh-CN");
        private readonly string _text;
        private readonly TextRunProperties _props;

        public StringTextSource(string text, TextRunProperties props)
        {
            _text = text;
            _props = props;
        }

        public override TextRun GetTextRun(int textSourceCharacterIndex)
        {
            if (textSourceCharacterIndex >= _text.Length)
                return new TextEndOfParagraph(1);

            int i = textSourceCharacterIndex;
            char c = _text[i];
            if (c == '\r')
            {
                int w = (i + 1 < _text.Length && _text[i + 1] == '\n') ? 2 : 1;
                return new TextEndOfLine(w);
            }
            if (c == '\n')
                return new TextEndOfLine(1);

            int end = i;
            while (end < _text.Length && _text[end] != '\r' && _text[end] != '\n') end++;
            return new TextCharacters(_text, i, end - i, _props);
        }

        public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int textSourceCharacterIndexLimit)
            => new(0, new CultureSpecificCharacterBufferRange(Culture, new CharacterBufferRange(string.Empty, 0, 0)));

        public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex)
            => throw new NotSupportedException();
    }
}
