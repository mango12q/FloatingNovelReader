using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.ViewModels;

public sealed partial class BookmarkListViewModel : ObservableObject
{
    private readonly BookmarkService _bookmark;
    private readonly DatabaseService _db;

    [ObservableProperty] private Book? _book;
    public ObservableCollection<Bookmark> Items { get; } = new();

    public BookmarkListViewModel(BookmarkService bookmark, DatabaseService db)
    {
        _bookmark = bookmark;
        _db = db;
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
        var readerVm = App.Services.GetRequiredService<ReaderViewModel>();
        readerVm.JumpToProgress(b.ChapterId, b.PageNumber);
        var w = App.Services.GetRequiredService<Views.BookmarkWindow>();
        w.Close();
    }

    [RelayCommand]
    public void Remove(Bookmark? b)
    {
        if (b == null) return;
        _bookmark.Remove(b.Id);
        Items.Remove(b);
    }
}
