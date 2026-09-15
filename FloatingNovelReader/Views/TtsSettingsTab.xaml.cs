using System.Windows.Controls;

namespace FloatingNovelReader.Views;

/// <summary>
/// 设置页「朗读」Tab。DataContext 由 SettingsWindow 注入 <c>TtsPanelViewModel</c>。
/// </summary>
public partial class TtsSettingsTab : UserControl
{
    public TtsSettingsTab()
    {
        InitializeComponent();
    }
}
