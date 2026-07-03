using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 章节目录弹窗 VM。
/// </summary>
public sealed partial class ChapterListViewModel : ObservableObject
{
    private readonly BookshelfService _bookshelf;
    private readonly ReadingSessionService _session;

    [ObservableProperty] private Book? _book;
    public ObservableCollection<Volume> Volumes { get; } = new();

    public ChapterListViewModel(BookshelfService bookshelf, ReadingSessionService session)
    {
        _bookshelf = bookshelf;
        _session = session;
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
        var readerVm = App.Services.GetRequiredService<ReaderViewModel>();
        readerVm.JumpToChapter(chapter, 0);
        var w = App.Services.GetRequiredService<Views.ChapterListWindow>();
        w.Close();
    }
}
