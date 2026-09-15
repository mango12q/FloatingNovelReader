using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 章节目录弹窗 VM。
/// 通过 <see cref="IPageAdvancer"/> 驱动跳转、通过 <see cref="CloseRequested"/> 请求关闭自己，
/// 不再反向解析 ReaderViewModel / ChapterListWindow（体检报告 §1.1）。
/// </summary>
public sealed partial class ChapterListViewModel : ObservableObject
{
    private readonly BookshelfService _bookshelf;
    private readonly ReadingSessionService _session;
    private readonly IPageAdvancer _reader;

    /// <summary>请求关闭承载本 VM 的窗口；由 View 订阅后 Close()。</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty] private Book? _book;
    public ObservableCollection<Volume> Volumes { get; } = new();

    public ChapterListViewModel(
        BookshelfService bookshelf,
        ReadingSessionService session,
        IPageAdvancer reader)
    {
        _bookshelf = bookshelf;
        _session = session;
        _reader = reader;
    }

    /// <summary>
    /// 加载一本书的完整结构到 VM。
    /// 接受可能仅有元数据（无 Volumes/Chapters）的 Book 也会自动从数据库补全，
    /// 这样调用方不用关心当前 Book 实例的完整度。
    /// </summary>
    public void Load(Book? book)
    {
        Book = book;
        Volumes.Clear();
        if (book == null) return;
        // 如果传入的 Book 还没有 Volume/Chapter, 从数据库补全
        var full = (book.Volumes != null && book.Volumes.Count > 0)
            ? book
            : _bookshelf.GetBookWithChapters(book.Id);
        if (full == null) return;
        Book = full;
        foreach (var v in full.Volumes) Volumes.Add(v);
    }

    [RelayCommand]
    public void JumpTo(Chapter? chapter)
    {
        if (chapter == null) return;
        _reader.JumpToChapter(chapter, 0);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
