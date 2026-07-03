using System.Windows;
using System.Windows.Controls;

namespace FloatingNovelReader.Controls;

public partial class OverlayControlBar : UserControl
{
    public event RoutedEventHandler? SettingsClick;
    public event RoutedEventHandler? CloseClick;
    public event RoutedEventHandler? MenuChapterListClick;
    public event RoutedEventHandler? MenuBookmarkListClick;
    public event RoutedEventHandler? MenuSettingsClick;

    public OverlayControlBar()
    {
        InitializeComponent();
    }

    public void SetInfo(string bookTitle, string chapter)
    {
        BookTitleText.Text = bookTitle ?? "";
        ChapterText.Text = chapter ?? "";
    }

    // Button.Click 是 RoutedEventHandler(object, RoutedEventArgs)，
    // 这里转发到我们的 RoutedEventHandler 事件，调用方按 RoutedEventArgs 接住。
    private void OnSettingsClick(object sender, RoutedEventArgs e)
        => SettingsClick?.Invoke(this, e);

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => CloseClick?.Invoke(this, e);

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        // 显示 ContextMenu（Button 自带 ContextMenu，但默认不自动弹）
        if (MenuBtn.ContextMenu != null)
        {
            MenuBtn.ContextMenu.PlacementTarget = MenuBtn;
            MenuBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            MenuBtn.ContextMenu.IsOpen = true;
        }
    }

    private void OnMenuChapterList(object sender, RoutedEventArgs e)
        => MenuChapterListClick?.Invoke(this, e);

    private void OnMenuBookmarkList(object sender, RoutedEventArgs e)
        => MenuBookmarkListClick?.Invoke(this, e);

    private void OnMenuSettings(object sender, RoutedEventArgs e)
        => MenuSettingsClick?.Invoke(this, e);
}
