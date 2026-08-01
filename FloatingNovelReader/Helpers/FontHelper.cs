using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 系统字体枚举与查询。
/// 中文字体识别：不靠名称猜，直接查字体是否含 CJK 字形（U+4E2D「中」），
/// 楷体/宋体/黑体/行楷/等线/方正/思源等任何中文字体都会被正确识别。
/// </summary>
public sealed class FontHelper
{
    private const int CjkProbeCodepoint = 0x4E2D; // 「中」

    // 常用中文字体置顶顺序（名称前缀匹配，中英系统语言都兼容）
    private static readonly string[] PinnedFontPrefixes =
    {
        "Microsoft YaHei", "微软雅黑",
        "SimSun", "NSimSun", "宋体", "新宋体",
        "SimHei", "黑体",
        "KaiTi", "楷体",
        "FangSong", "仿宋",
        "DengXian", "等线",
    };

    /// <summary>
    /// 枚举全部系统字体，中文字体排前面（常用置顶），西文字体按名称排在后面。
    /// </summary>
    public IReadOnlyList<string> GetFontFamiliesForPicker()
    {
        var chinese = new List<string>();
        var western = new List<string>();

        foreach (var f in Fonts.SystemFontFamilies)
        {
            string name;
            try
            {
                name = f.Source;
            }
            catch
            {
                continue; // 跳过无法读取的字体
            }

            if (SupportsCjk(f))
                chinese.Add(name);
            else
                western.Add(name);
        }

        var pinned = new List<string>();
        var otherChinese = new List<string>();
        foreach (var name in chinese)
        {
            if (PinnedFontPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                pinned.Add(name);
            else
                otherChinese.Add(name);
        }

        pinned.Sort(PinnedComparer.Instance);
        otherChinese.Sort(StringComparer.OrdinalIgnoreCase);
        western.Sort(StringComparer.OrdinalIgnoreCase);

        var result = new List<string>(pinned.Count + otherChinese.Count + western.Count);
        result.AddRange(pinned);
        result.AddRange(otherChinese);
        result.AddRange(western);
        return result;
    }

    /// <summary>字体是否支持中文（含 CJK 基本字形）。检测失败按西文处理，不丢弃。</summary>
    private static bool SupportsCjk(FontFamily family)
    {
        try
        {
            foreach (var typeface in family.FamilyTypefaces)
            {
                var tf = new Typeface(family, typeface.Style, typeface.Weight, typeface.Stretch);
                if (tf.TryGetGlyphTypeface(out var glyph) &&
                    glyph.CharacterToGlyphMap.ContainsKey(CjkProbeCodepoint))
                {
                    return true;
                }
            }
        }
        catch
        {
            // 个别字体读取失败，按西文处理
        }
        return false;
    }

    private sealed class PinnedComparer : IComparer<string>
    {
        public static readonly PinnedComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            return RankOf(x).CompareTo(RankOf(y));
        }

        private static int RankOf(string? name)
        {
            if (name == null) return int.MaxValue;
            for (int i = 0; i < PinnedFontPrefixes.Length; i++)
            {
                if (name.StartsWith(PinnedFontPrefixes[i], StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return int.MaxValue;
        }
    }
}
