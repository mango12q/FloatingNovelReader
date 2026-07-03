using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;

namespace FloatingNovelReader.ViewModels;

public sealed partial class BookshelfViewModel : ObservableObject
{
    private readonly BookshelfService _bookshelf;
    private readonly BookImportService _importer;
    private readonly SettingsService _settings;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sortBy = "LastReadTime";
    [ObservableProperty] private bool _isListView;

    public ObservableCollection<Book> Books { get; } = new();
    public Array SortOptions { get; } = new[] { "LastReadTime", "ImportTime", "Title" };

    public BookshelfViewModel(
        BookshelfService bookshelf,
        BookImportService importer,
        SettingsService settings)
    {
        _bookshelf = bookshelf;
        _importer = importer;
        _settings = settings;
    }

    [RelayCommand]
    public void Refresh()
    {
        _bookshelf.Reload(SearchText, SortBy);
        Books.Clear();
        foreach (var b in _bookshelf.Books) Books.Add(b);
    }

    [RelayCommand]
    public async void Import()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "TXT 文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            Multiselect = true,
            Title = "选择要导入的 TXT 文件",
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            try
            {
                await _importer.ImportAsync(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{path}\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        Refresh();
    }

    [RelayCommand]
    public void Remove(Book? book)
    {
        if (book == null) return;
        // 三选一: YesNoCancel, 默认 No
        //   Yes  = 删除记录 + 删除源 .txt 文件 (彻底)
        //   No   = 仅删除数据库记录, 保留源文件
        //   Cancel = 取消
        var msg = $"确定从书架移除《{book.Title}》?\n\n" +
                  "[是] 删除数据库记录 + 删除源文件 (彻底移除, 不可恢复)\n" +
                  "[否] 仅删除数据库记录, 源文件保留\n" +
                  "[取消] 放弃操作";
        var r = MessageBox.Show(msg, "确认移除", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return;
        var deleteFile = r == MessageBoxResult.Yes;
        try
        {
            _bookshelf.Remove(book.Id, deleteFile);
            Books.Remove(book);
            var extra = deleteFile ? "，并删除源文件" : "";
            MessageBox.Show($"已移除《{book.Title}》{extra}。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"移除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void Open(Book? book)
    {
        if (book == null) return;
        var readerVm = App.Services.GetRequiredService<ReaderViewModel>();
        var fullBook = _bookshelf.GetBookWithChapters(book.Id);
        if (fullBook == null) return;
        readerVm.LoadBook(fullBook);

        var w = App.Services.GetRequiredService<Views.ReaderWindow>();
        w.Show();
        w.Activate();
    }

    [RelayCommand]
    public void SearchChanged()
    {
        Refresh();
    }

    [RelayCommand]
    public void ChangeCoverColor(Book? book)
    {
        if (book == null) return;
        // 简化版：循环预设
        var colors = new[] { "#6C8CFF", "#FF6B6B", "#51CF66", "#FCC419", "#845EF7", "#20C997", "#FFA94D" };
        var current = Array.IndexOf(colors, book.CoverColor);
        book.CoverColor = colors[(current + 1) % colors.Length];
        // 简单做法：直接更新 DB（增量）
        // 这里暂不实现 UpdateBookCover，避免引入过多代码
        Refresh();
    }
}
