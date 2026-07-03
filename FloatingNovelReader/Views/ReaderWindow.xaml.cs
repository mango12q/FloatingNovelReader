using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.Views;

/// <summary>
/// 阅读窗口 View。只负责 UI 生命周期事件、窗口交互（拖动/缩放/控制栏动画）
/// 和控件事件路由。所有业务逻辑（热键分发、翻页、章节跳转等）均在 ReaderViewModel 中处理。
/// </summary>
public partial class ReaderWindow : Window
{
    private readonly ReaderViewModel _vm;
    private readonly WindowBehaviorService _windowBehavior;
    private bool _isDragging;

    public ReaderWindow(ReaderViewModel vm, WindowBehaviorService windowBehavior)
    {
        InitializeComponent();
        _vm = vm;
        _windowBehavior = windowBehavior;
        DataContext = _vm;

        _windowBehavior.Attach(this);
        _windowBehavior.ApplyTopmost(true);

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.ApplyTextAreaSize(TextArea.ActualWidth, TextArea.ActualHeight);
        TopBar.SetInfo(_vm.BookTitle, _vm.ChapterTitle);
        BottomBar.SetInfo($"{_vm.CurrentPage + 1}/{_vm.TotalPages}", "");

        // 从 ReadingProgress 恢复窗口位置 / 大小
        if (_vm.CurrentBook != null)
        {
            var p = App.Services.GetRequiredService<DatabaseService>().GetProgress(_vm.CurrentBook.Id);
            if (p != null)
            {
                if (p.WindowWidth > 0) Width = p.WindowWidth;
                if (p.WindowHeight > 0) Height = p.WindowHeight;
                if (!double.IsNaN(p.WindowLeft) && !double.IsNaN(p.WindowTop))
                {
                    Left = p.WindowLeft;
                    Top = p.WindowTop;
                }
                if (p.Opacity > 0) Opacity = p.Opacity;
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
            _vm.ApplyTextAreaSize(TextArea.ActualWidth, TextArea.ActualHeight);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        _vm.SaveWindowState(Left, Top, Width, Height, Opacity);
        Hide();
    }

    // ── 窗口拖动（View 层 UI 行为）───────────────────────────────
    // RootBorder 的 MouseLeftButtonDown（Bubble 阶段）：
    //   - 按钮（⚙/×）的 OnMouseLeftButtonDown 会 raise Click 然后 handled=true
    //     → 不会冒泡到 Border → 不拖动
    //   - 文本区、空白区没 handled → 冒泡到 Border 触发拖动
    //   - 边缘 5px 走 WindowChrome.ResizeBorderThickness 由 WPF 处理为原生 resize
    private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.Handled) return;
        if (_isDragging) return;
        _isDragging = true;
        try
        {
            DragMove();
            _windowBehavior.ApplyEdgeSnap(new Point(Left, Top));
        }
        catch { /* DragMove 在鼠标释放前会抛 InvalidOperation */ }
        finally { _isDragging = false; }
    }

    // ── 控制栏显示 / 隐藏动画（纯 UI 逻辑，保留在 View 层）─────────
    private void OnTopAreaEnter(object sender, MouseEventArgs e)
    {
        ShowBar(TopBar);
        ScheduleHide(TopHitArea, () => HideBar(TopBar));
    }
    private void OnTopAreaLeave(object sender, MouseEventArgs e) { }
    private void OnBottomAreaEnter(object sender, MouseEventArgs e)
    {
        ShowBar(BottomBar);
        ScheduleHide(BottomHitArea, () => HideBar(BottomBar));
    }
    private void OnBottomAreaLeave(object sender, MouseEventArgs e) { }

    private void ShowBar(UIElement bar)
    {
        var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(300));
        bar.BeginAnimation(OpacityProperty, anim);
    }
    private void HideBar(UIElement bar)
    {
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
        bar.BeginAnimation(OpacityProperty, anim);
    }
    private void ScheduleHide(FrameworkElement area, Action onHide)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (s, e) => { timer.Stop(); if (!IsMouseOver) onHide(); };
        timer.Start();
    }

    // ── 控制栏按钮事件（纯 UI 路由）───────────────────────────────
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var w = App.Services.GetRequiredService<SettingsWindow>();
        w.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnMenuChapterListClick(object sender, RoutedEventArgs e)
        => _vm.ShowChapterListCommand.Execute(null);

    private void OnMenuBookmarkListClick(object sender, RoutedEventArgs e)
        => _vm.ShowBookmarkListCommand.Execute(null);
}
