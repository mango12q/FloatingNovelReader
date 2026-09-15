using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FloatingNovelReader.Services;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 「分页」子 VM：文本区尺寸、当前页、总页数、当前页文本，以及分页引擎的调用。
///
/// 从 ReaderViewModel 拆出（体检报告 §1.2）。它**不知道**书 / 章 / 热键 / 会话的存在，
/// 只认"一段章节文本"，因此可以脱离整个阅读流程单独测试。
/// 页码或页文本变化时抛出 <see cref="PageChanged"/>，由父 VM 去更新状态栏、阅读百分比与会话。
/// </summary>
public sealed partial class ReaderPagerViewModel : ObservableObject
{
    private readonly PaginationService _paginator;
    private readonly SettingsService _settings;
    private List<PaginationService.PageRange> _pages = new();

    // 与 ReaderWindow.xaml 中 PageTextView 的 Padding="12,8" 保持一致：
    // 分页测量的可用区域必须减去内边距，否则每页末尾的行会被渲染裁剪
    private const double TextPaddingHorizontal = 24; // 12 * 2
    private const double TextPaddingVertical = 16;   // 8 * 2

    [ObservableProperty] private int _currentPage;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private string _pageText = string.Empty;
    [ObservableProperty] private double _textAreaWidth = 480;
    [ObservableProperty] private double _textAreaHeight = 600;

    /// <summary>页码或页文本已更新。等价于原先 UpdatePage() 的调用点。</summary>
    public event EventHandler? PageChanged;

    public ReaderPagerViewModel(PaginationService paginator, SettingsService settings)
    {
        _paginator = paginator;
        _settings = settings;
    }

    public int PageCount => _pages.Count;

    /// <summary>重算分页（章节文本变化 / 尺寸变化）。</summary>
    public void Recompute(string chapterText)
    {
        if (string.IsNullOrEmpty(chapterText))
        {
            // 与原先一致：空文本只清空页状态，不触发 PageChanged（不覆盖状态栏）
            _pages = new List<PaginationService.PageRange>();
            TotalPages = 0;
            PageText = string.Empty;
            return;
        }

        var s = _settings.Current.Display;
        // 减去 TextBlock 内边距，测量区域与真实排版区域一致
        var w = Math.Max(50, TextAreaWidth - TextPaddingHorizontal);
        var h = Math.Max(50, TextAreaHeight - TextPaddingVertical);

        _pages = _paginator.Paginate(
            chapterText,
            s.FontFamily, s.FontSize, s.LineHeight, w, h,
            s.FontBold ? FontWeights.Bold : FontWeights.Normal,
            s.FontItalic ? FontStyles.Italic : FontStyles.Normal);

        TotalPages = _pages.Count;
        if (CurrentPage >= TotalPages) CurrentPage = Math.Max(0, TotalPages - 1);
        RefreshPageText(chapterText);
    }

    /// <summary>跳到指定页（自动夹到合法范围）。</summary>
    public void SetPage(int page, string chapterText)
    {
        CurrentPage = Math.Clamp(page, 0, Math.Max(0, _pages.Count - 1));
        RefreshPageText(chapterText);
    }

    /// <summary>本章内前进一页；返回 false 表示已是本章最后一页（由调用方决定是否跨章）。</summary>
    public bool TryMoveNext(string chapterText)
    {
        if (CurrentPage >= _pages.Count - 1) return false;
        CurrentPage++;
        RefreshPageText(chapterText);
        return true;
    }

    /// <summary>本章内后退一页；返回 false 表示已是本章第一页（由调用方决定是否跨章）。</summary>
    public bool TryMovePrevious(string chapterText)
    {
        if (CurrentPage <= 0) return false;
        CurrentPage--;
        RefreshPageText(chapterText);
        return true;
    }

    /// <summary>文本区尺寸变化；变化过小时不重算分页。</summary>
    public void ApplyTextAreaSize(double w, double h, string chapterText)
    {
        if (Math.Abs(w - TextAreaWidth) < 0.5 && Math.Abs(h - TextAreaHeight) < 0.5) return;
        TextAreaWidth = w;
        TextAreaHeight = h;
        // 使用增量判断：尺寸变化微小时不重算分页
        if (_paginator.InvalidateIfSizeChanged(w, h))
            Recompute(chapterText);
    }

    /// <summary>显示设置变化：清缓存后重算。</summary>
    public void InvalidateAndRecompute(string chapterText)
    {
        _paginator.ClearCache();
        Recompute(chapterText);
    }

    private void RefreshPageText(string chapterText)
    {
        if (_pages.Count == 0)
        {
            PageText = string.Empty;
            return;
        }

        var range = _pages[Math.Min(CurrentPage, _pages.Count - 1)];
        PageText = chapterText.Substring(
            range.Start,
            Math.Min(range.Length, chapterText.Length - range.Start));
        PageChanged?.Invoke(this, EventArgs.Empty);
    }
}
