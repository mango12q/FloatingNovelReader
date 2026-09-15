using System.Windows;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.Core;

/// <summary>
/// 窗口导航抽象。
///
/// 取代此前散落在 Service / ViewModel 里的 <c>App.Services.GetRequiredService&lt;XxxWindow&gt;()</c>：
/// 那些调用把 DI 容器降级成了全局 Service Locator，并让业务层反向依赖具体的 WPF 窗口类型
/// （体检报告 §1.1 / §1.3 / §1.4）。现在"解析窗口"这件事只发生在
/// <see cref="WindowNavigator"/> 一处，其余代码只认这个接口，单测丢个假实现就能跑。
/// </summary>
public interface IWindowNavigator
{
    /// <summary>阅读窗口；尚未创建时为 null。仅用于给对话框设置 Owner。</summary>
    Window? ActiveReaderWindow { get; }

    /// <summary>阅读窗口当前是否可见（F8 的"隐藏/恢复"用它判断，避免 VM 直接摸 Window.Visibility）。</summary>
    bool IsReaderVisible { get; }

    /// <summary>把书装入阅读窗口并显示。等价于旧的 LoadBook + Show + Activate 三步。</summary>
    void OpenReader(Book book, ReadingProgress? progress = null);

    /// <summary>显示已存在的阅读窗口（不改当前书）。</summary>
    void ShowReader();

    /// <summary>隐藏阅读窗口（Boss Key）。</summary>
    void HideReader();

    /// <summary>显示书架窗口。</summary>
    void ShowBookshelf();

    /// <summary>显示设置窗口（模态）。</summary>
    void ShowSettingsDialog(Window? owner = null);

    /// <summary>
    /// 创建章节目录对话框：已注入 DI、已 Load 该书、已设置 Owner，调用方只需 ShowDialog。
    /// 返回 <see cref="Window"/> 而非具体类型，避免 VM 依赖 View。
    /// </summary>
    Window CreateChapterListDialog(Book book, Window? owner);

    /// <summary>创建书签列表对话框（同上）。</summary>
    Window CreateBookmarkListDialog(Book book, Window? owner);
}
