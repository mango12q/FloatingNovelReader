using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Microsoft.Win32;
using Serilog;

namespace FloatingNovelReader.ViewModels;

public sealed partial class BookshelfViewModel : ObservableObject
{
    private readonly BookshelfService _bookshelf;
    private readonly BookImportService _importer;
    private readonly SettingsService _settings;
    private readonly IWindowNavigator _navigator;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sortBy = "LastReadTime";
    [ObservableProperty] private bool _isListView;

    public ObservableCollection<Book> Books { get; } = new();
    public Array SortOptions { get; } = new[] { "LastReadTime", "ImportTime", "Title" };

    public BookshelfViewModel(
        BookshelfService bookshelf,
        BookImportService importer,
        SettingsService settings,
        IWindowNavigator navigator,
        IDialogService dialogs)
    {
        _bookshelf = bookshelf;
        _importer = importer;
        _settings = settings;
        _navigator = navigator;
        _dialogs = dialogs;
    }

    [RelayCommand]
    public void Refresh()
    {
        _bookshelf.Reload(SearchText, SortBy);
        Books.Clear();
        foreach (var b in _bookshelf.Books) Books.Add(b);
    }

    [RelayCommand]
    public async Task ImportAsync()
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
                _dialogs.Error($"导入失败：{path}\n{ex.Message}", "错误");
            }
        }
        Refresh();
    }

    public async System.Threading.Tasks.Task ImportFileAsync(string path)
    {
        await _importer.ImportAsync(path);
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
        var r = _dialogs.AskWithCancel(msg, "确认移除");
        if (r == DialogChoice.Cancel) return;
        var deleteFile = r == DialogChoice.Yes;
        try
        {
            _bookshelf.Remove(book.Id, deleteFile);
            Books.Remove(book);
            var extra = deleteFile ? "，并删除源文件" : "";
            _dialogs.Info($"已移除《{book.Title}》{extra}。", "完成");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"移除失败：{ex.Message}", "错误");
        }
    }

    [RelayCommand]
    public void Open(Book? book)
    {
        if (book == null) return;
        var fullBook = _bookshelf.GetBookWithChapters(book.Id);
        if (fullBook == null) return;
        _navigator.OpenReader(fullBook);
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
        var next = colors[(current + 1) % colors.Length];
        // 先写库：否则 Refresh() 从数据库重载列表时，会把内存里的改色覆盖回旧值
        _bookshelf.UpdateCoverColor(book.Id, next);
        // Book 没有实现 INotifyPropertyChanged，只能靠重建集合让 UI 刷新
        Refresh();
    }
}
