using System.Windows;
using FloatingNovelReader.Core;

namespace FloatingNovelReader.Services;

/// <summary>
/// <see cref="IDialogService"/> 的 WPF 实现。所有 MessageBox 调用集中在这里。
/// </summary>
public sealed class DialogService : IDialogService
{
    public void Info(string message, string title = "") =>
        MessageBox.Show(
            message,
            string.IsNullOrEmpty(title) ? Constants.AppName : title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    public void Error(string message, string title = "") =>
        MessageBox.Show(
            message,
            string.IsNullOrEmpty(title) ? "错误" : title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    public DialogChoice Ask(string message, string title) =>
        ToChoice(MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question));

    public DialogChoice AskWithCancel(string message, string title) =>
        ToChoice(MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question));

    private static DialogChoice ToChoice(MessageBoxResult r) => r switch
    {
        MessageBoxResult.Yes => DialogChoice.Yes,
        MessageBoxResult.No => DialogChoice.No,
        _ => DialogChoice.Cancel,
    };
}
