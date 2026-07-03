using System.Windows;
using System.Windows.Input;
using FloatingNovelReader;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FloatingNovelReader.Views;

public partial class ChapterListWindow : Window
{
    private readonly ChapterListViewModel _vm;

    public ChapterListWindow(ChapterListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += (s, e) =>
        {
            if (_vm.Book != null)
            {
                BookTitleText.Text = $"《{_vm.Book.Title}》 章节目录";
                VolumeList.ItemsSource = _vm.Volumes;
            }
        };
    }

    private void OnChapterClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Chapter ch)
        {
            _vm.JumpToCommand.Execute(ch);
        }
    }
}
