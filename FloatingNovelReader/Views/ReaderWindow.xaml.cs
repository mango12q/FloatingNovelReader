using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using Serilog;

namespace FloatingNovelReader.Views;

public partial class ReaderWindow : Window
{
    private readonly ReaderViewModel _vm;
    private readonly WindowBehaviorService _windowBehavior;
    private readonly IWindowNavigator _navigator;
    private Point _dragStart;
    private bool _isDragging;
    private const double DragThreshold = 5.0;
    private readonly DispatcherTimer _idleCursorTimer;
    private bool _isFadingPage;

    public ReaderWindow(
        ReaderViewModel vm,
        WindowBehaviorService windowBehavior,
        IWindowNavigator navigator)
    {
        InitializeComponent();
        _vm = vm;
        _windowBehavior = windowBehavior;
        _navigator = navigator;
        DataContext = _vm;

        _windowBehavior.Attach(this);
        _windowBehavior.ApplyTopmost(true);

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ReaderViewModel.ReadingPercent) && IsLoaded)
                UpdateProgressBarWidth();
        };

        // 页文本 / 页码现在归分页子 VM 所有，因此订阅它的变更
        _vm.Pager.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ReaderPagerViewModel.PageText) && !_isFadingPage && IsLoaded)
                AnimatePageTextFade();

            // 底栏页码原本只在 OnLoaded 里写过一次，翻页后永远停在旧值
            // （状态栏会更新，两个页码显示互相矛盾）。这里跟随 CurrentPage/TotalPages 更新。
            if (IsLoaded && (e.PropertyName == nameof(ReaderPagerViewModel.CurrentPage)
                             || e.PropertyName == nameof(ReaderPagerViewModel.TotalPages)))
                BottomBar.SetInfo($"{_vm.Pager.CurrentPage + 1}/{_vm.Pager.TotalPages}", "");
        };

        _idleCursorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _idleCursorTimer.Tick += (s, e) =>
        {
            _idleCursorTimer.Stop();
            if (_windowBehavior.ClickThrough != ClickThroughState.ClickThrough && IsMouseOver)
                Mouse.OverrideCursor = Cursors.None;
        };

        MouseMove += (s, e) =>
        {
            Mouse.OverrideCursor = null;
            _idleCursorTimer.Stop();
            _idleCursorTimer.Start();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.ApplyTextAreaSize(TextArea.ActualWidth, TextArea.ActualHeight);
        TopBar.SetInfo(_vm.BookTitle, _vm.ChapterTitle);
        BottomBar.SetInfo($"{_vm.Pager.CurrentPage + 1}/{_vm.Pager.TotalPages}", "");

        if (_vm.CurrentBook != null)
        {
            // 窗口几何来自 VM 已经读到的进度，View 不再自己解析 DatabaseService
            var p = _vm.SavedProgress;
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

        ApplyHighContrast();

        if (_windowBehavior.ClickThrough != ClickThroughState.ClickThrough)
            _idleCursorTimer.Start();
    }

    private void ApplyHighContrast()
    {
        if (SystemParameters.HighContrast)
        {
            TextArea.Background = SystemColors.WindowBrush;
            ProgressFill.Background = SystemColors.HighlightBrush;
        }
        UpdateProgressBarWidth();
    }

    private void UpdateProgressBarWidth()
    {
        if (!IsLoaded || ProgressBarHost == null) return;
        var pct = _vm.ReadingPercent;
        var w = ProgressBarHost.ActualWidth * Math.Max(0, Math.Min(1, pct));
        if (w < 1 && pct > 0) w = 1;
        ProgressFill.Width = w;
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

    private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.Handled) return;
        if (_isDragging) return;
        _isDragging = true;
        _dragStart = e.GetPosition(this);
        try
        {
            DragMove();
            _windowBehavior.ApplyEdgeSnap(new Point(Left, Top));
        }
        catch { }
        finally { _isDragging = false; }
    }

    private void OnBorderMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_windowBehavior.ClickThrough == ClickThroughState.ClickThrough) return;
        _vm.PrevPageCommand.Execute(null);
        e.Handled = true;
    }

    private void OnBorderMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_windowBehavior.ClickThrough == ClickThroughState.ClickThrough) return;
        if (e.Delta > 0)
            _vm.PrevPageCommand.Execute(null);
        else if (e.Delta < 0)
            _vm.NextPageCommand.Execute(null);
        e.Handled = true;
    }

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

    private void AnimatePageTextFade()
    {
        _isFadingPage = true;
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (s, e) =>
        {
            PageTextView.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            PageTextView.BeginAnimation(OpacityProperty, fadeIn);
            fadeIn.Completed += (s2, e2) => { PageTextView.Opacity = 1; _isFadingPage = false; };
        };
        PageTextView.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _navigator.ShowSettingsDialog(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    private void OnMenuChapterListClick(object sender, RoutedEventArgs e)
        => _vm.ShowChapterListCommand.Execute(null);

    private void OnMenuAddBookmarkClick(object sender, RoutedEventArgs e)
        => _vm.AddBookmarkCommand.Execute(null);

    private void OnMenuBookmarkListClick(object sender, RoutedEventArgs e)
        => _vm.ShowBookmarkListCommand.Execute(null);

    private void OnMenuSpeakFromHereClick(object sender, RoutedEventArgs e)
        => _vm.SpeakFromHereCommand.Execute(null);

    private void OnMenuStopSpeakingClick(object sender, RoutedEventArgs e)
        => _vm.StopSpeakingCommand.Execute(null);
}
