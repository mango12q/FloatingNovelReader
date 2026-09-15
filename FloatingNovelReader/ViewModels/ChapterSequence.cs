using System;
using System.Collections.Generic;
using System.Linq;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 章节序列的纯查询逻辑。
///
/// 原来 ReaderViewModel 的翻页/跳章方法里，"取全书扁平章节 → FindIndex → 判断边界"
/// 这套代码重复了 4 遍，且和分页、会话、状态栏混在一个方法里，无法单测。
/// 抽到这里之后是可断言的纯函数（含"章节不在当前书里"这类边界）。
/// </summary>
public static class ChapterSequence
{
    public static IReadOnlyList<Chapter> Flatten(Book? book) =>
        book?.FlatChapters().ToList() ?? (IReadOnlyList<Chapter>)Array.Empty<Chapter>();

    public static int IndexOf(IReadOnlyList<Chapter> chapters, int? chapterId)
    {
        if (chapterId is null || chapters.Count == 0) return -1;
        for (var i = 0; i < chapters.Count; i++)
        {
            if (chapters[i].Id == chapterId.Value) return i;
        }
        return -1;
    }

    /// <summary>下一章；已是最后一章或找不到当前章时返回 null。</summary>
    public static Chapter? Next(IReadOnlyList<Chapter> chapters, int? currentId)
    {
        var idx = IndexOf(chapters, currentId);
        return idx >= 0 && idx < chapters.Count - 1 ? chapters[idx + 1] : null;
    }

    /// <summary>上一章；已是第一章或找不到当前章时返回 null。</summary>
    public static Chapter? Previous(IReadOnlyList<Chapter> chapters, int? currentId)
    {
        var idx = IndexOf(chapters, currentId);
        return idx > 0 ? chapters[idx - 1] : null;
    }

    public static bool IsLast(IReadOnlyList<Chapter> chapters, int? currentId)
    {
        var idx = IndexOf(chapters, currentId);
        return idx >= 0 && idx == chapters.Count - 1;
    }

    /// <summary>
    /// 阅读百分比 = (当前章序号 + 本章内进度) / 总章数。
    /// 章节不在序列中时返回 0。
    /// </summary>
    public static double ReadingPercent(IReadOnlyList<Chapter> chapters, int? currentId, int page, int totalPages)
    {
        var idx = IndexOf(chapters, currentId);
        if (idx < 0 || chapters.Count == 0) return 0;
        var withinChapter = (page + 1.0) / Math.Max(1, totalPages);
        return (idx + withinChapter) / Math.Max(1, chapters.Count);
    }
}
