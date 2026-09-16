using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // for ColorDialog
using System.Windows.Input;
using System.Windows.Media;
using FloatingNovelReader;
using FloatingNovelReader.ViewModels;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FloatingNovelReader.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly SettingsService _settingsService;
    private readonly TtsPanelViewModel _ttsPanel;

    public SettingsWindow(
        SettingsViewModel vm,
        SettingsService settingsService,
        TtsPanelViewModel ttsPanel)
    {
        InitializeComponent();
        _vm = vm;
        _settingsService = settingsService;
        _ttsPanel = ttsPanel;
        DataContext = _vm;
        TtsTab.DataContext = _ttsPanel;
        Loaded += OnLoadedInternal;
        Closing += OnClosingInternal;
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        var list = new List<HotkeyItem>();
        foreach (var kv in _vm.Current.Hotkeys.GlobalHotkeys)
            list.Add(new HotkeyItem(kv.Key, kv.Value, DisplayNameOf(kv.Key)));
        HotkeyList.ItemsSource = list;

        ApplyHighContrast();

        // 声音列表异步加载：缓存命中几乎瞬时；联网失败会退回内置列表并在界面提示
        _ = _ttsPanel.LoadVoicesCommand.ExecuteAsync(null);
    }

    private void ApplyHighContrast()
    {
        if (SystemParameters.HighContrast)
            Background = SystemColors.WindowBrush;
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
        "SpeakFromHere" => "从当前开始朗读",
        "SpeakFromHereMinutes" => "朗读 N 分钟",
        "SpeakFromHereChapters" => "朗读 N 章",
        "StopSpeaking" => "停止朗读",
        _ => action
    };

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var list = (List<HotkeyItem>)HotkeyList.ItemsSource;
        _vm.Current.Hotkeys.GlobalHotkeys.Clear();
        foreach (var item in list)
            _vm.Current.Hotkeys.GlobalHotkeys[item.Action] = item.Key;
        _vm.Save();
        _ttsPanel.StopPreviewCommand.Execute(null);
        SetDialogResult(true);
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _ttsPanel.StopPreviewCommand.Execute(null);
        _vm.Cancel();
        _ttsPanel.Refresh();   // Reload 换掉了 Current，把朗读页拉回实际设置
        SetDialogResult(false);
    }

    private void OnClosingInternal(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 试听用的是同一个 TtsService：不在关闭时收尾，关掉设置窗后声音还在响
        _ttsPanel.StopPreviewCommand.Execute(null);

        if (!IsSaved)
        {
            _vm.Cancel();
            _ttsPanel.Refresh();
        }
    }

    private bool IsSaved { get; set; }

    /// <summary>
    /// 只有用 ShowDialog 打开时才能设 DialogResult。
    /// 冒烟宿主（fnr-seed --show-settings）用 Show() 承载本窗口，
    /// 直接赋值会抛"DialogResult 只能在创建窗口并调用 ShowDialog 后设置"。
    /// </summary>
    private void SetDialogResult(bool value)
    {
        try
        {
            DialogResult = value;
        }
        catch (InvalidOperationException)
        {
            // 非 ShowDialog 宿主：忽略即可
        }
        IsSaved = value;
        Close();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _vm.ResetToDefaultCommand.Execute(null);
        _ttsPanel.Refresh();   // Reset 也换掉了 Current
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsSvc = _settingsService;
            var suggestedDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "FloatingNovelReader_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            var result = settingsSvc.ExportSettings(suggestedDir);
            System.Windows.MessageBox.Show(
                $"设置已导出到：\n{suggestedDir}\n\n包含：settings.json + library.db",
                "导出设置成功",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"导出设置失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "导入设置将覆盖当前所有设置（显示、快捷键、自动阅读等）。\n\n确定要导入吗？",
            "导入设置确认",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.OK) return;

        try
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择包含 settings.json 的设置备份文件夹",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var settingsSvc = _settingsService;
            settingsSvc.ImportSettings(dlg.SelectedPath);
            _vm.Current = settingsSvc.Current;
            _vm.AutoReadIntervalSec = settingsSvc.Current.AutoReadIntervalSec;

            System.Windows.MessageBox.Show("设置已成功导入！", "导入完成", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"导入设置失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnNumberOnly(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    private void OnPasteNumberOnly(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(System.Windows.Forms.DataFormats.Text))
        {
            var text = e.DataObject.GetData(System.Windows.Forms.DataFormats.Text) as string;
            if (!int.TryParse(text, out _))
                e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
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
