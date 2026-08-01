using System.Linq;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

public class FontHelperTests
{
    private readonly FontHelper _helper = new();

    [Fact]
    public void GetFontFamiliesForPicker_NotEmpty()
    {
        var fonts = _helper.GetFontFamiliesForPicker();
        Assert.NotEmpty(fonts);
    }

    [Fact]
    public void GetFontFamiliesForPicker_ChineseBeforeWestern()
    {
        var fonts = _helper.GetFontFamiliesForPicker().ToList();
        var simsunIdx = fonts.IndexOf("SimSun");
        var arialIdx = fonts.IndexOf("Arial");
        Assert.True(simsunIdx >= 0, "系统字体列表中应包含 SimSun");
        Assert.True(arialIdx >= 0, "系统字体列表中应包含 Arial");
        Assert.True(simsunIdx < arialIdx, "中文字体应排在西文字体之前");
    }
}
