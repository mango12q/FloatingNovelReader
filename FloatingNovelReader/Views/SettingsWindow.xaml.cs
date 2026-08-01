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

        UpdateCustomColorPanelVisibility();
        BackgroundCombo.SelectionChanged += (s, e) => UpdateCustomColorPanelVisibility();

        var list = new List<HotkeyItem>();
        foreach (var kv in _vm.Current.Hotkeys.GlobalHotkeys)
            list.Add(new HotkeyItem(kv.Key, kv.Value, DisplayNameOf(kv.Key)));
        HotkeyList.ItemsSource = list;

        ApplyHighContrast();
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
        _ => action
    };

    private void UpdateCustomColorPanelVisibility()
    {
        if (BackgroundCombo == null || CustomColorPanel == null) return;
        var isCustom = _vm.Current.Display.BackgroundPreset == Models.BackgroundPreset.Custom;
        CustomColorPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPickCustomColor(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.ColorDialog();
        if (!string.IsNullOrEmpty(_vm.Current.Display.CustomBackgroundColor))
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(_vm.Current.Display.CustomBackgroundColor);
                dlg.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
            }
            catch { }
        }
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            _vm.Current.Display.CustomBackgroundColor = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            _vm.Current.Display.BackgroundPreset = Models.BackgroundPreset.Custom;
            CustomColorHex.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
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

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _vm.ResetToDefaultCommand.Execute(null);
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsSvc = App.Services.GetRequiredService<SettingsService>();
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

            var settingsSvc = App.Services.GetRequiredService<SettingsService>();
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
