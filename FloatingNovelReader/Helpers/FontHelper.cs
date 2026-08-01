using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace FloatingNovelReader.Helpers;

/// <summary>字体选择器条目：源名用于渲染，显示名用于 UI。</summary>
public sealed record FontOption(string FamilyName, string DisplayName, bool IsInstalled);

/// <summary>
/// 系统字体枚举与查询。
/// 中文字体识别：不靠名称猜，直接查字体是否含 CJK 字形（U+4E2D「中」）。
/// 常用中文字体（宋体/楷体/黑体/仿宋/行楷等）始终置顶显示中文名，
/// 未安装的（Win10/11 可选字体功能，如行楷）标注「未安装」置灰，选中后 WPF 回退到默认字体。
/// </summary>
public sealed class FontHelper
{
    private const int CjkProbeCodepoint = 0x4E2D; // 「中」

    // 常用中文字体：源名（可能多个别名）→ 中文显示名。Win10/11 可选字体（楷体/行楷等）未安装时标注。
    private static readonly (string[] Aliases, string ChineseName)[] CommonFonts =
    {
        (new[] { "Microsoft YaHei UI", "Microsoft YaHei" }, "微软雅黑"),
        (new[] { "SimSun" }, "宋体"),
        (new[] { "NSimSun" }, "新宋体"),
        (new[] { "SimHei" }, "黑体"),
        (new[] { "KaiTi" }, "楷体"),
        (new[] { "FangSong" }, "仿宋"),
        (new[] { "DengXian" }, "等线"),
        (new[] { "STXingkai" }, "行楷"),
    };

    /// <summary>
    /// 字体选择器条目：常用中文字体置顶（中文显示名，未安装标注），
    /// 其余系统字体按 CJK 字形检测分中文/西文排序排在后面。
    /// </summary>
    public IReadOnlyList<FontOption> GetFontOptionsForPicker()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            installed.Add(name);
            if (SupportsCjk(f))
                chinese.Add(name);
            else
                western.Add(name);
        }

        var result = new List<FontOption>();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (aliases, chineseName) in CommonFonts)
        {
            var found = aliases.FirstOrDefault(installed.Contains);
            foreach (var a in aliases) covered.Add(a);
            if (found != null)
                result.Add(new FontOption(found, chineseName, true));
            else
                result.Add(new FontOption(aliases[0], $"{chineseName}（未安装）", false));
        }

        chinese.Sort(StringComparer.OrdinalIgnoreCase);
        western.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var name in chinese.Where(n => !covered.Contains(n)))
            result.Add(new FontOption(name, name, true));
        foreach (var name in western)
            result.Add(new FontOption(name, name, true));

        return result;
    }

    /// <summary>枚举全部系统字体源名（中文字体排前面），供简单字符串场景使用。</summary>
    public IReadOnlyList<string> GetFontFamiliesForPicker()
    {
        return GetFontOptionsForPicker()
            .Where(o => o.IsInstalled)
            .Select(o => o.FamilyName)
            .ToList();
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
}
