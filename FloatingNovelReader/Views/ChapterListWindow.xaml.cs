using System.Windows;
using System.Windows.Input;
using FloatingNovelReader;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FloatingNovelReader.Views;

public partial class ChapterListWindow : Window
{
    // 首选/最小尺寸（DIP）。700 DIP 高在 150% 缩放的 1080p 屏上（工作区约 1280×680 DIP）
    // 放不下，DpiHelper 会按工作区收窄，保证整窗可见、可拖动。
    private const double PreferredWidth = 500;
    private const double PreferredHeight = 700;
    private const double MinWidthDip = 400;
    private const double MinHeightDip = 500;

    private readonly ChapterListViewModel _vm;

    public ChapterListWindow(ChapterListViewModel vm)
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
                BookTitleText.Text = $"《{_vm.Book.Title}》 章节目录";
                VolumeList.ItemsSource = _vm.Volumes;
            }
        };

        SourceInitialized += (s, e) => ApplyDpiLayout(recenter: true);
        DpiChanged += (s, e) => ApplyDpiLayout(recenter: false);
    }

    private void ApplyDpiLayout(bool recenter) =>
        DpiHelper.ApplyAdaptiveLayout(
            this, Owner, PreferredWidth, PreferredHeight, MinWidthDip, MinHeightDip, recenter);

    private void OnChapterClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Chapter ch)
        {
            _vm.JumpToCommand.Execute(ch);
        }
    }
}
