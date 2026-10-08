using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// 「跳转到页码」输入校验。页码是本章内的页序（与状态栏「3/12」同一套编号）。
/// </summary>
public class PageJumpInputTests
{
    [Theory]
    [InlineData("1", 12, 1)]
    [InlineData("12", 12, 12)]
    [InlineData(" 7 ", 12, 7)]
    public void TryParse_AcceptsPagesInsideRange(string input, int total, int expected)
    {
        Assert.True(PageJumpInput.TryParse(input, total, out var page, out var error));
        Assert.Equal(expected, page);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("13")]
    [InlineData("999")]
    public void TryParse_RejectsOutOfRange(string input)
    {
        Assert.False(PageJumpInput.TryParse(input, 12, out var page, out var error));
        Assert.Equal(0, page);
        Assert.Contains("12", error);   // 错误提示里要带上真实范围
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("3.5")]
    [InlineData("一")]
    public void TryParse_RejectsNonNumericInput(string? input)
    {
        Assert.False(PageJumpInput.TryParse(input, 12, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_WhenChapterHasNoPages_TellsUserToPaginateFirst()
    {
        Assert.False(PageJumpInput.TryParse("1", 0, out _, out var error));
        Assert.Contains("分页", error);
    }
}
