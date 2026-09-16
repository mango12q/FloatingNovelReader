using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

    /// <summary>「N 分钟」「N 章」只接受整数（与设置页其他数字框行为一致）。</summary>
    private void OnNumberOnly(object sender, TextCompositionEventArgs e) =>
        e.Handled = !int.TryParse(e.Text, out _);

    private void OnPasteNumberOnly(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.DataObject.GetDataPresent(DataFormats.Text)
            ? e.DataObject.GetData(DataFormats.Text) as string
            : null;

        if (text is null || !int.TryParse(text, out _)) e.CancelCommand();
    }
}
