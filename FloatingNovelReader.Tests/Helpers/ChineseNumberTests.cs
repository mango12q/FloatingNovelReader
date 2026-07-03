using System.Linq;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// 中文数字解析的单元测试。
/// </summary>
public class ChineseNumberTests
{
    [Theory]
    [InlineData("一", 1)]
    [InlineData("二", 2)]
    [InlineData("十", 10)]
    [InlineData("十一", 11)]
    [InlineData("二十", 20)]
    [InlineData("二十一", 21)]
    [InlineData("九十九", 99)]
    [InlineData("一百", 100)]
    [InlineData("一百二十三", 123)]
    [InlineData("九百九十九", 999)]
    [InlineData("一千", 1000)]
    [InlineData("一万", 10000)]
    public void ParseChineseNumber_Basic(string input, int expected)
    {
        var n = ChapterParser.ParseChineseNumber(input);
        Assert.Equal(expected, n);
    }
}
