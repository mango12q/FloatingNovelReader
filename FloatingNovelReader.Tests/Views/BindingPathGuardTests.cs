using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FloatingNovelReader.ViewModels;
using Xunit;

namespace FloatingNovelReader.Tests.Views;

/// <summary>
/// XAML 绑定路径守卫。
///
/// WPF 的绑定路径写错**不会编译报错**，也**不会运行时报错** —— 只是静默不生效（界面空白或
/// 控件不更新）。对一个没有 UI 自动化测试的桌面应用来说，这是最危险的一类回归。
/// 这个测试把每个 <c>{Binding X}</c> 的路径按反射解析到窗口 DataContext 对应的 VM 类型上，
/// 重命名/删除属性会立刻失败。
///
/// 注意：<c>DataTemplate</c> 内部的绑定针对的是列表项类型（Book / Volume / Chapter / FontOption…），
/// 不是窗口 VM，所以整棵子树跳过。
/// </summary>
public class BindingPathGuardTests
{
    private static readonly Dictionary<string, Type> WindowToViewModel = new()
    {
        ["ReaderWindow.xaml"] = typeof(ReaderViewModel),
        ["BookshelfWindow.xaml"] = typeof(BookshelfViewModel),
        ["SettingsWindow.xaml"] = typeof(SettingsViewModel),
        ["ChapterListWindow.xaml"] = typeof(ChapterListViewModel),
        ["BookmarkWindow.xaml"] = typeof(BookmarkListViewModel),
        // 设置页里的朗读 Tab 是独立 UserControl，DataContext 是 TtsPanelViewModel
        ["TtsSettingsTab.xaml"] = typeof(TtsPanelViewModel),
    };

    public static IEnumerable<object[]> Cases =>
        WindowToViewModel.Select(kv => new object[] { kv.Key });

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryBindingPathResolvesOnItsViewModel(string xamlFileName)
    {
        var vmType = WindowToViewModel[xamlFileName];
        var path = Path.Combine(AppContext.BaseDirectory, "Views", xamlFileName);
        Assert.True(File.Exists(path), $"未找到 XAML 副本: {path}（检查 Tests.csproj 的 None/CopyToOutputDirectory）");

        var doc = XDocument.Load(path);
        var failures = new List<string>();

        foreach (var element in doc.Descendants())
        {
            if (IsInsideDataTemplate(element)) continue;

            foreach (var attr in element.Attributes())
            {
                foreach (Match m in Regex.Matches(attr.Value, @"\{Binding\s+([^},]*)").Cast<Match>())
                {
                    var bindingPath = m.Groups[1].Value.Trim();
                    if (bindingPath.Length == 0) continue;      // {Binding} = 绑 DataContext 自身
                    if (bindingPath.Contains('=')) continue;    // {Binding ElementName=X, Path=Y} 等

                    if (!Resolves(vmType, bindingPath))
                    {
                        failures.Add(
                            $"{xamlFileName}: <{element.Name.LocalName} {attr.Name.LocalName}=\"{attr.Value.Trim()}\"> " +
                            $"→ 路径 '{bindingPath}' 在 {vmType.Name} 上不存在");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0,
            "以下 XAML 绑定路径解析不到 VM 属性（绑定会静默失效）：" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static bool IsInsideDataTemplate(XElement element)
    {
        for (var e = element; e != null; e = e.Parent)
        {
            if (e.Name.LocalName.EndsWith("DataTemplate", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static bool Resolves(Type type, string path)
    {
        var current = (Type?)type;
        foreach (var segment in path.Split('.'))
        {
            if (current == null) return false;
            var prop = current.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;
            current = prop.PropertyType;
        }
        return true;
    }
}
