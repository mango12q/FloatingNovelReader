using System;
using System.Windows;
using FloatingNovelReader.Helpers;

namespace FloatingNovelReader.Views;

/// <summary>
/// 「跳转到页码」输入框（本章内页码，与状态栏「3/12」同一套编号）。
/// 输入校验在 <see cref="PageJumpInput"/> 里，这里只负责界面与 DPI 适配。
/// </summary>
public partial class PageJumpWindow : Window
{
    private const double PreferredWidth = 340;
    private const double PreferredHeight = 190;
    private const double MinWidthDip = 300;
    private const double MinHeightDip = 170;

    private int _totalPages;

    /// <summary>用户确认的页码（1 起）。取消时不会被读取。</summary>
    public int SelectedPage { get; private set; } = 1;

    public PageJumpWindow()
    {
        InitializeComponent();

        // 与书签/目录窗口同一套高分屏适配
        SourceInitialized += (s, e) => ApplyDpiLayout();
        DpiChanged += (s, e) => ApplyDpiLayout();
        Loaded += OnLoadedInternal;
    }

    /// <summary>由 <see cref="Core.WindowNavigator"/> 在 ShowDialog 之前调用。</summary>
    public void Configure(int currentPage, int totalPages)
    {
        _totalPages = Math.Max(0, totalPages);
        SelectedPage = Math.Clamp(currentPage, 1, Math.Max(1, _totalPages));
        HintText.Text = $"本章共 {_totalPages} 页，输入 1 ~ {_totalPages} 之间的页码";
        PageBox.Text = SelectedPage.ToString();
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        PageBox.Focus();
        PageBox.SelectAll();
    }

    private void ApplyDpiLayout() =>
        DpiHelper.ApplyAdaptiveLayout(
            this, Owner, PreferredWidth, PreferredHeight, MinWidthDip, MinHeightDip);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!PageJumpInput.TryParse(PageBox.Text, _totalPages, out var page, out var error))
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            PageBox.Focus();
            PageBox.SelectAll();
            return;
        }

        SelectedPage = page;
        DialogResult = true;
    }
}
