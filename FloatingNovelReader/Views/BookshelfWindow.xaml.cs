using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FloatingNovelReader.Views;

public partial class BookshelfWindow : Window
{
    private readonly BookshelfViewModel _vm;

    public BookshelfWindow(BookshelfViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Loaded += OnFirstLoaded;
        Closed += (s, e) => { /* 不退出进程 */ };
    }

    private bool _firstLoad;
    private void OnFirstLoaded(object? sender, RoutedEventArgs e)
    {
        if (_firstLoad) return;
        _firstLoad = true;
        _vm.Refresh();
        BooksList.ItemsSource = _vm.Books;
        UpdateEmptyState();

        if (SystemParameters.HighContrast)
            Background = SystemColors.WindowBrush;
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        _vm.ImportCommand.Execute(null);
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DragOverBorder.Visibility = Visibility.Visible;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            DragOverBorder.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DragOverBorder.Visibility = Visibility.Collapsed;
        if (!e.Data.GetDataPresent(System.Windows.Forms.DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var txtFiles = files.Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (txtFiles.Length == 0) return;

        foreach (var path in txtFiles)
        {
            try
            {
                await _vm.ImportFileAsync(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{path}\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        _vm.Refresh();
        UpdateEmptyState();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var w = App.Services.GetRequiredService<SettingsWindow>();
        w.Owner = this;
        w.ShowDialog();
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        // XAML 初始化过程中 SortCombo 字段可能尚未回写，防御性跳过
        if (SortCombo == null || _vm == null) return;
        _vm.SortBy = (SortCombo.SelectedIndex) switch
        {
            0 => "LastReadTime",
            1 => "ImportTime",
            2 => "Title",
            _ => "LastReadTime"
        };
        _vm.Refresh();
        UpdateEmptyState();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox == null || _vm == null) return;
        _vm.SearchText = SearchBox.Text;
        _vm.Refresh();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        if (EmptyHint == null) return;
        EmptyHint.Visibility = _vm.Books.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBookClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Book b)
        {
            _vm.OpenCommand.Execute(b);
        }
    }

    private void OnBookRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.Tag is not Book b) return;
        var book = b;
        var menu = new ContextMenu();
        var mi1 = new MenuItem { Header = "开始阅读" };
        mi1.Click += (s, ev) => _vm.OpenCommand.Execute(book);
        var mi2 = new MenuItem { Header = "修改封面颜色" };
        mi2.Click += (s, ev) => _vm.ChangeCoverColorCommand.Execute(book);
        var mi3 = new MenuItem { Header = "从书架移除" };
        mi3.Click += (s, ev) => _vm.RemoveCommand.Execute(book);
        menu.Items.Add(mi1);
        menu.Items.Add(mi2);
        menu.Items.Add(mi3);
        menu.IsOpen = true;
    }
}
