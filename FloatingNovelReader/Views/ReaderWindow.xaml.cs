using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FloatingNovelReader;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.Views;

public partial class ReaderWindow : Window
{
    private readonly ReaderViewModel _vm;
    private readonly HotkeyManager _hotkey;
    private readonly WindowBehaviorService _windowBehavior;
    private readonly AutoReadService _autoRead;
    private readonly ReadingSessionService _session;
    private readonly SettingsService _settings;
    private DispatcherTimer? _topHideTimer;
    private DispatcherTimer? _bottomHideTimer;
    private bool _isDragging;

    public ReaderWindow(
        ReaderViewModel vm,
        HotkeyManager hotkey,
        WindowBehaviorService windowBehavior,
        AutoReadService autoRead,
        ReadingSessionService session,
        SettingsService settings)
    {
        InitializeComponent();
        _vm = vm;
        _hotkey = hotkey;
        _windowBehavior = windowBehavior;
        _autoRead = autoRead;
        _session = session;
        _settings = settings;
        DataContext = _vm;

        _hotkey.HotkeyPressed += OnHotkey;
        _hotkey.Start();

        _windowBehavior.Attach(this);
        _windowBehavior.ApplyTopmost(true);

        Loaded += (s, e) =>
        {
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
            // 边缘 resize 由 WindowChrome.ResizeBorderThickness 自动处理
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.ApplyTextAreaSize(TextArea.ActualWidth, TextArea.ActualHeight);
        TopBar.SetInfo(_vm.BookTitle, _vm.ChapterTitle);
        BottomBar.SetInfo($"{_vm.CurrentPage + 1}/{_vm.TotalPages}", "");
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
            _vm.ApplyTextAreaSize(TextArea.ActualWidth, TextArea.ActualHeight);
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded) return;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        _session.SaveProgress(Left, Top, Width, Height, Opacity);
        Hide();
    }

    // RootBorder 的 MouseLeftButtonDown（Bubble 阶段）：
    // - 按钮（⚙/×）的 OnMouseLeftButtonDown 会 raise Click 然后 handled=true → 不会冒泡到 Border → 不拖动
    // - 文本区、空白区没 handled → 冒泡到 Border 触发拖动
    // - 边缘 5px 走 WindowChrome.ResizeBorderThickness 由 WPF 处理为原生 resize
    private void OnBorderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.Handled) return; // 子元素已处理（Button.Click 等）→ 不进入拖动
        if (_isDragging) return;
        _isDragging = true;
        try
        {
            DragMove();
            _windowBehavior.ApplyEdgeSnap(new Point(Left, Top));
        }
        catch { /* DragMove 在鼠标释放前会抛 InvalidOperation */ }
        finally
        {
            _isDragging = false;
        }
    }

    // 顶/底部控制栏显示
    private void OnTopAreaEnter(object sender, MouseEventArgs e)
    {
        ShowBar(TopBar);
        ScheduleHide(TopHitArea, _topHideTimer, () => HideBar(TopBar));
    }
    private void OnTopAreaLeave(object sender, MouseEventArgs e) { }
    private void OnBottomAreaEnter(object sender, MouseEventArgs e)
    {
        ShowBar(BottomBar);
        ScheduleHide(BottomHitArea, _bottomHideTimer, () => HideBar(BottomBar));
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
    private void ScheduleHide(FrameworkElement area, DispatcherTimer? timer, Action onHide)
    {
        timer?.Stop();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            if (!IsMouseOver) onHide();
        };
        timer.Start();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var w = App.Services.GetRequiredService<SettingsWindow>();
        w.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnMenuChapterListClick(object sender, RoutedEventArgs e)
        => ShowChapterList();

    private void OnMenuBookmarkListClick(object sender, RoutedEventArgs e)
        => ShowBookmarkList();

    // --- 全局快捷键处理 ---
    private void OnHotkey(object? sender, HotkeyAction action)
    {
        if (!IsVisible) return;
        Dispatcher.Invoke(() =>
        {
            switch (action)
            {
                case HotkeyAction.NextPage: _vm.NextPageCommand.Execute(null); break;
                case HotkeyAction.PrevPage: _vm.PrevPageCommand.Execute(null); break;
                case HotkeyAction.NextChapter: _vm.NextChapterCommand.Execute(null); break;
                case HotkeyAction.PrevChapter: _vm.PrevChapterCommand.Execute(null); break;
                case HotkeyAction.IncreaseOpacity: _windowBehavior.IncreaseOpacity(); break;
                case HotkeyAction.DecreaseOpacity: _windowBehavior.DecreaseOpacity(); break;
                case HotkeyAction.ToggleClickThrough: _windowBehavior.ToggleClickThrough(); break;
                case HotkeyAction.ToggleTopmost: _windowBehavior.ToggleTopmost(); break;
                case HotkeyAction.ToggleAutoRead: _vm.ToggleAutoRead(); break;
                case HotkeyAction.AutoReadFaster: _autoRead.Faster(); break;
                case HotkeyAction.AutoReadSlower: _autoRead.Slower(); break;
                case HotkeyAction.HideWindow: Hide(); break;
                case HotkeyAction.ShowChapterList: ShowChapterList(); break;
                case HotkeyAction.ShowBookmarkList: ShowBookmarkList(); break;
                case HotkeyAction.AddBookmark: _vm.AddBookmark(); break;
            }
        });
    }

    private void ShowChapterList()
    {
        if (_vm.CurrentBook == null)
        {
            ShowStatus("未加载图书");
            return;
        }
        var w = App.Services.GetRequiredService<ChapterListWindow>();
        if (w.DataContext is ChapterListViewModel cvm)
        {
            // ChapterListViewModel.Load 内部会从数据库补全 Volumes/Chapters, 所以这里直接传 CurrentBook 即可
            cvm.Load(_vm.CurrentBook);
        }
        w.Owner = this;
        w.ShowDialog();
    }

    private void ShowBookmarkList()
    {
        if (_vm.CurrentBook == null)
        {
            ShowStatus("未加载图书");
            return;
        }
        var w = App.Services.GetRequiredService<BookmarkWindow>();
        // BookmarkWindow 接受 BookmarkListViewModel, 用 Load 注入当前书
        if (w.DataContext is BookmarkListViewModel bvm)
        {
            bvm.Load(_vm.CurrentBook);
        }
        w.Owner = this;
        w.ShowDialog();
    }

    private DispatcherTimer? _statusTimer;
    private void ShowStatus(string text, int ms = 2000)
    {
        _vm.StatusText = text;
        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        _statusTimer.Tick += (s, e) =>
        {
            _statusTimer?.Stop();
            // 还原为标准状态
            if (_vm.CurrentChapter != null)
                _vm.StatusText = $"{_vm.CurrentChapter.Title}    {_vm.CurrentPage + 1}/{_vm.TotalPages}";
        };
        _statusTimer.Start();
    }
}
