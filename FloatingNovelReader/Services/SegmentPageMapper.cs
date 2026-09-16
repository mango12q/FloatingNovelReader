using System;

namespace FloatingNovelReader.Services;

/// <summary>
/// 「段在章里的比例」→ 页码（0 基，已夹到合法范围）。
///
/// 朗读时阅读位置要跟随音频，但音频的粒度是"段"、屏幕的粒度是"页"，
/// 两者不可能逐字对齐，只能按比例近似。抽成纯函数后可以穷举边界单测：
/// 首页不会变成 -1、末段不会越界、段数/页数为 0 时不会除零。
/// </summary>
public static class SegmentPageMapper
{
    public static int PageForSegment(int segmentIndex, int segmentCount, int totalPages)
    {
        if (totalPages <= 0) return 0;
        if (segmentCount <= 0) return 0;

        var index = segmentIndex;
        if (index < 0) index = 0;
        if (index > segmentCount - 1) index = segmentCount - 1;

        // 第 index+1 段结束时落在本章的比例位置，再换算成页码
        var page = (int)Math.Floor((index + 1.0) / segmentCount * totalPages) - 1;
        return Math.Clamp(page, 0, totalPages - 1);
    }
}
