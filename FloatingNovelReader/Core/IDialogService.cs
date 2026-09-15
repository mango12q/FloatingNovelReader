namespace FloatingNovelReader.Core;

/// <summary>三态选择结果（不带 WPF 类型，便于单测）。</summary>
public enum DialogChoice
{
    Yes,
    No,
    Cancel,
}

/// <summary>
/// 对话框抽象。
///
/// 此前 ViewModel 直接调 <c>MessageBox.Show</c>（体检报告 §1.6，共 21 处次），
/// 导致 VM 一旦离开 WPF 环境就无法单测（NullRef）。抽出来之后 VM 只依赖本接口。
/// </summary>
public interface IDialogService
{
    void Info(string message, string title = "");
    void Error(string message, string title = "");

    /// <summary>是 / 否。</summary>
    DialogChoice Ask(string message, string title);

    /// <summary>是 / 否 / 取消——需要区分"取消"与"否"时必须用这个。</summary>
    DialogChoice AskWithCancel(string message, string title);
}
