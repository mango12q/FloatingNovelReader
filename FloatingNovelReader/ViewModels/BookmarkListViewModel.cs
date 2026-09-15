using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;

namespace FloatingNovelReader.ViewModels;

public sealed partial class BookmarkListViewModel : ObservableObject
{
    private readonly BookmarkService _bookmark;
    private readonly DatabaseService _db;
    private readonly IPageAdvancer _reader;

    /// <summary>请求关闭承载本 VM 的窗口；由 View 订阅后 Close()。</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty] private Book? _book;
    public ObservableCollection<Bookmark> Items { get; } = new();

    public BookmarkListViewModel(
        BookmarkService bookmark,
        DatabaseService db,
        IPageAdvancer reader)
    {
        _bookmark = bookmark;
        _db = db;
        _reader = reader;
    }

    public void Load(Book book)
    {
        Book = book;
        Items.Clear();
        foreach (var b in _bookmark.List(book.Id)) Items.Add(b);
    }

    [RelayCommand]
    public void Jump(Bookmark? b)
    {
        if (b == null) return;
        _reader.JumpToProgress(b.ChapterId, b.PageNumber);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Remove(Bookmark? b)
    {
        if (b == null) return;
        _bookmark.Remove(b.Id);
        Items.Remove(b);
    }
}
