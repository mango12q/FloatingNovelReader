using System.Windows;
using System.Windows.Input;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FloatingNovelReader.Views;

public partial class BookmarkWindow : Window
{
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
    }

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
