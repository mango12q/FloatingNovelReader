using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FloatingNovelReader;
using FloatingNovelReader.ViewModels;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += OnLoadedInternal;
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        FontFamilyCombo.ItemsSource = _vm.FontFamilies;
        if (!_vm.FontFamilies.Contains(_vm.Current.Display.FontFamily) && _vm.FontFamilies.Count > 0)
            FontFamilyCombo.SelectedIndex = 0;
        else
            FontFamilyCombo.SelectedItem = _vm.Current.Display.FontFamily;

        // 填充快捷键列表
        var list = new List<HotkeyItem>();
        foreach (var kv in _vm.Current.Hotkeys.GlobalHotkeys)
            list.Add(new HotkeyItem(kv.Key, kv.Value, DisplayNameOf(kv.Key)));
        HotkeyList.ItemsSource = list;
    }

    private static string DisplayNameOf(string action) => action switch
    {
        "NextPage" => "下一页",
        "PrevPage" => "上一页",
        "NextChapter" => "下一章",
        "PrevChapter" => "上一章",
        "IncreaseOpacity" => "增加透明度",
        "DecreaseOpacity" => "降低透明度",
        "ToggleClickThrough" => "切换鼠标穿透",
        "ToggleTopmost" => "切换窗口置顶",
        "ToggleAutoRead" => "切换自动阅读",
        "AutoReadFaster" => "加快自动阅读",
        "AutoReadSlower" => "减慢自动阅读",
        "HideWindow" => "隐藏窗口 (Boss Key)",
        "ShowChapterList" => "章节目录",
        "AddBookmark" => "添加书签",
        "ShowBookmarkList" => "书签列表",
        "TogglePause" => "暂停",
        _ => action
    };

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // 回写快捷键
        var list = (List<HotkeyItem>)HotkeyList.ItemsSource;
        _vm.Current.Hotkeys.GlobalHotkeys.Clear();
        foreach (var item in list)
            _vm.Current.Hotkeys.GlobalHotkeys[item.Action] = item.Key;
        _vm.Save();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _vm.Cancel();
        DialogResult = false;
        Close();
    }

    public class HotkeyItem
    {
        public string Action { get; }
        public string Key { get; set; }
        public string DisplayName { get; }
        public HotkeyItem(string action, string key, string displayName)
        {
            Action = action;
            Key = key;
            DisplayName = displayName;
        }
    }
}
