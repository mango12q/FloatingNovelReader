using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 「弹窗」子 VM：章节目录 / 书签列表的开关管理。
///
/// 从 ReaderViewModel 拆出（体检报告 §1.2）。它只关心"窗口开着还是关着"，
/// 不认识翻页、分页、热键。状态栏提示文本由调用方写入，
/// 这样它不需要反向引用父 VM（避免循环依赖）。
/// </summary>
public sealed partial class ReaderDialogsViewModel : ObservableObject
{
    private readonly IWindowNavigator _navigator;
    private Window? _chapterListWindow;
    private Window? _bookmarkWindow;

    public ReaderDialogsViewModel(IWindowNavigator navigator) => _navigator = navigator;

    /// <summary>切换章节目录弹窗（已开则关）。返回需要写入状态栏的提示；null 表示不动状态栏。</summary>
    public string? ToggleChapterList(Book book)
    {
        if (_chapterListWindow is { IsVisible: true })
        {
            _chapterListWindow.Close();
            _chapterListWindow = null;
            return "章节目录已关闭";
        }

        var w = _navigator.CreateChapterListDialog(book, _navigator.ActiveReaderWindow);
        w.Closed += (s, e) => { _chapterListWindow = null; };
        _chapterListWindow = w;
        w.ShowDialog();
        return null;
    }

    /// <summary>切换书签列表弹窗（已开则关）。</summary>
    public string? ToggleBookmarkList(Book book)
    {
        if (_bookmarkWindow is { IsVisible: true })
        {
            _bookmarkWindow.Close();
            _bookmarkWindow = null;
            return "书签列表已关闭";
        }

        var w = _navigator.CreateBookmarkListDialog(book, _navigator.ActiveReaderWindow);
        w.Closed += (s, e) => { _bookmarkWindow = null; };
        _bookmarkWindow = w;
        w.ShowDialog();
        return null;
    }
}
