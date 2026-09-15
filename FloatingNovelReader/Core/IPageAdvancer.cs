using FloatingNovelReader.Models;

namespace FloatingNovelReader.Core;

/// <summary>
/// 「驱动翻页」的最小契约。
///
/// <c>ReaderViewModel</c> 实现它；自动阅读、以及以后的 TTS / OCR 朗读都通过这个接口驱动翻页，
/// 不必认识 <c>ReaderViewModel</c> 的内部实现，也不必往它的构造函数里继续塞依赖
/// （体检报告 §1.2 指出的 God Class 扩张问题）。
/// </summary>
public interface IPageAdvancer
{
    /// <summary>是否已装入一本书。</summary>
    bool HasBook { get; }

    /// <summary>当前章节的完整文本（TTS 切段的输入）。</summary>
    string CurrentChapterText { get; }

    bool CanGoNextPage { get; }
    bool CanGoPrevPage { get; }

    void NextPage();
    void PrevPage();
    void NextChapter();
    void PrevChapter();

    /// <summary>跳到指定章节的指定页。</summary>
    void JumpToChapter(Chapter chapter, int page = 0);

    /// <summary>按 chapterId 跳转（书签用）。</summary>
    void JumpToProgress(int chapterId, int page);
}
