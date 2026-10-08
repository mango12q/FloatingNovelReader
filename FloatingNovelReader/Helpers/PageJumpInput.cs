namespace FloatingNovelReader.Helpers;

/// <summary>
/// 「跳转到页码」输入校验（纯函数，方便单测）。
///
/// 页码是**本章内**的页序（与状态栏「3/12」同一套编号），不是 PDF 的物理页。
/// 分章粒度粗的时候（例如 PDF 无标题按页分块），用它比翻目录快。
/// </summary>
public static class PageJumpInput
{
    /// <summary>
    /// 解析用户输入。成功返回 1 起的页码，失败返回 false 并给出可显示的错误文案。
    /// </summary>
    public static bool TryParse(string? input, int totalPages, out int page, out string? error)
    {
        page = 0;
        error = null;

        if (totalPages <= 0)
        {
            error = "当前章节还没有分页，先翻一页试试。";
            return false;
        }

        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            error = $"请输入 1 ~ {totalPages} 之间的页码。";
            return false;
        }

        if (!int.TryParse(text, out var value))
        {
            error = "只能填数字，例如 12。";
            return false;
        }

        if (value < 1 || value > totalPages)
        {
            error = $"超出范围：本章共 {totalPages} 页。";
            return false;
        }

        page = value;
        return true;
    }
}
