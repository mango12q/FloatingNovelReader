namespace FloatingNovelReader.Services;

/// <summary>
/// 朗读会话的"宿主"：谁发起朗读，谁就提供跨章所需的章节导航能力。
///
/// 这样切分的原因是**依赖方向**：TtsService 需要「下一章」，
/// 但章节文本与阅读位置都在 ReaderViewModel 手里。与其让 Service 反向依赖 VM
/// （体检报告 §1.3 的分层反模式），不如把这点能力抽成接口，由 VM 实现。
/// 于是 TtsService 对 WPF / 数据库 / VM 依旧一无所知，可以整段单测。
///
/// 契约：三个方法都必须在 **UI 线程** 上被调用（<see cref="TtsService"/> 负责切线程后调用）。
/// </summary>
public interface ITtsPlaybackHost
{
    /// <summary>当前章节的完整文本；没有可读内容时返回空串。</summary>
    string GetCurrentChapterText();

    /// <summary>是否还有下一章（用于判断"停在全书末"）。</summary>
    bool CanAdvanceChapter();

    /// <summary>真正切到下一章（阅读窗口会翻到新章第 1 页），返回 false 表示切不过去。</summary>
    bool AdvanceToNextChapter();
}
