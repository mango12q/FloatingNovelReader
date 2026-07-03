using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// 系统字体枚举与查询。
/// </summary>
public sealed class FontHelper
{
    public IReadOnlyList<string> GetInstalledFontFamilies()
    {
        var list = new List<string>();
        foreach (var f in Fonts.SystemFontFamilies)
        {
            // 优先加入中文字体
            try
            {
                list.Add(f.Source);
            }
            catch
            {
                // 跳过无法读取的字体
            }
        }
        return list;
    }

    public IReadOnlyList<string> GetChineseFontFamilies()
    {
        var chinese = new List<string>();
        var all = GetInstalledFontFamilies();
        foreach (var f in all)
        {
            var lower = f.ToLowerInvariant();
            if (lower.Contains("yahei") || lower.Contains("simhei") || lower.Contains("simsun") ||
                lower.Contains("kaiti") || lower.Contains("fangsong") || lower.Contains("微软") ||
                lower.Contains("雅黑") || lower.Contains("黑体") || lower.Contains("宋体") ||
                lower.Contains("楷体") || lower.Contains("仿宋"))
            {
                chinese.Add(f);
            }
        }
        if (chinese.Count == 0) chinese.AddRange(all);
        return chinese;
    }
}
