using System;
using System.Windows;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using FloatingNovelReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.Core;

/// <summary>
/// <see cref="IWindowNavigator"/> 的实现。这里是全应用**唯一**允许从容器解析窗口的地方，
/// 把"取窗口"收敛到一个可替换的接缝上。异常在此统一兜底并记日志（与原实现的 try/catch 行为一致）。
/// </summary>
public sealed class WindowNavigator : IWindowNavigator
{
    private readonly IServiceProvider _services;

    public WindowNavigator(IServiceProvider services) => _services = services;

    private ReaderWindow? Reader => _services.GetService<ReaderWindow>();

    public Window? ActiveReaderWindow => Reader;

    public bool IsReaderVisible => Reader is { IsVisible: true };

    public void OpenReader(Book book, ReadingProgress? progress = null)
    {
        _services.GetRequiredService<ReaderViewModel>().LoadBook(book, progress);
        ShowReader();
    }

    public void ShowReader()
    {
        try
        {
            var w = Reader ?? throw new InvalidOperationException("ReaderWindow 未在容器中注册");
            if (!w.IsInitialized) w.InitializeComponent();
            w.Show();
            w.Activate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "显示阅读窗口失败");
        }
    }

    public void HideReader()
    {
        try
        {
            Reader?.Hide();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "隐藏阅读窗口失败");
        }
    }

    public void ShowBookshelf()
    {
        try
        {
            var w = _services.GetRequiredService<BookshelfWindow>();
            w.Show();
            w.Activate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "显示书架失败");
        }
    }

    public void ShowSettingsDialog(Window? owner = null)
    {
        try
        {
            var w = _services.GetRequiredService<SettingsWindow>();
            if (owner != null && !ReferenceEquals(owner, w)) w.Owner = owner;
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "显示设置失败");
        }
    }

    public Window CreateChapterListDialog(Book book, Window? owner)
    {
        var w = _services.GetRequiredService<ChapterListWindow>();
        if (w.DataContext is ChapterListViewModel vm) vm.Load(book);
        if (owner != null) w.Owner = owner;
        return w;
    }

    public Window CreateBookmarkListDialog(Book book, Window? owner)
    {
        var w = _services.GetRequiredService<BookmarkWindow>();
        if (w.DataContext is BookmarkListViewModel vm) vm.Load(book);
        if (owner != null) w.Owner = owner;
        return w;
    }
}
