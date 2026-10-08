using System.Windows;
using System.Windows.Input;
using FloatingNovelReader;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FloatingNovelReader.Views;

public partial class BookmarkWindow : Window
{
    // 首选/最小尺寸（DIP）。高分屏（150%/200% 缩放）上工作区会小于首选值，
    // 由 DpiHelper 夹进工作区，避免窗口比屏幕还高、底部书签点不到。
    private const double PreferredWidth = 500;
    private const double PreferredHeight = 600;
    private const double MinWidthDip = 400;
    private const double MinHeightDip = 400;

    private readonly BookmarkListViewModel _vm;

    public BookmarkWindow(BookmarkListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        // VM 只发"请求关闭"事件，窗口自己关自己：VM 不必反向持有 View
        _vm.CloseRequested += (s, e) => Close();
        Loaded += (s, e) =>
        {
            if (_vm.Book != null)
            {
                BookTitleText.Text = $"《{_vm.Book.Title}》 书签列表";
                BookmarkList.ItemsSource = _vm.Items;
            }
        };

        // 句柄一建好就按所在显示器的 DPI 与工作区适配（此时 Owner 已由 WindowNavigator 设好），
        // 早于首次布局，不会看到窗口先跳一下再归位。
        SourceInitialized += (s, e) => ApplyDpiLayout(recenter: true);
        // 拖到另一块 DPI 不同的显示器：保持窗口位置，只重新夹进工作区
        DpiChanged += (s, e) => ApplyDpiLayout(recenter: false);
    }

    private void ApplyDpiLayout(bool recenter) =>
        DpiHelper.ApplyAdaptiveLayout(
            this, Owner, PreferredWidth, PreferredHeight, MinWidthDip, MinHeightDip, recenter);

    private void OnBookmarkClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Bookmark b)
        {
            _vm.JumpCommand.Execute(b);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Bookmark b)
        {
            _vm.RemoveCommand.Execute(b);
        }
    }
}
