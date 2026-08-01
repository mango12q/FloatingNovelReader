using System.Linq;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

public class FontHelperTests
{
    private readonly FontHelper _helper = new();

    [Fact]
    public void GetFontOptionsForPicker_NotEmpty()
    {
        var fonts = _helper.GetFontOptionsForPicker();
        Assert.NotEmpty(fonts);
    }

    [Fact]
    public void GetFontOptionsForPicker_CommonChineseFontsPinnedOnTop()
    {
        var fonts = _helper.GetFontOptionsForPicker().ToList();
        var firstEight = fonts.Take(8).Select(f => f.DisplayName).ToList();
        Assert.Equal("微软雅黑", firstEight[0]);
        Assert.Equal("宋体", firstEight[1]);
        Assert.Equal("新宋体", firstEight[2]);
        Assert.Equal("黑体", firstEight[3]);
        Assert.Equal("楷体", firstEight[4]);
        Assert.Equal("仿宋", firstEight[5]);
        Assert.Equal("等线", firstEight[6]);
        Assert.StartsWith("行楷", firstEight[7]);
    }

    [Fact]
    public void GetFontOptionsForPicker_CommonFontsUseChineseDisplayName()
    {
        var fonts = _helper.GetFontOptionsForPicker().ToList();
        var simsun = fonts.First(f => f.FamilyName == "SimSun");
        Assert.Equal("宋体", simsun.DisplayName);
    }

    [Fact]
    public void GetFontOptionsForPicker_UninstalledCommonFontMarked()
    {
        var fonts = _helper.GetFontOptionsForPicker().ToList();
        foreach (var f in fonts.Take(8))
        {
            if (!f.IsInstalled)
                Assert.EndsWith("（未安装）", f.DisplayName);
        }
    }

    [Fact]
    public void GetFontOptionsForPicker_ChineseBeforeWestern()
    {
        var fonts = _helper.GetFontOptionsForPicker().ToList();
        var simsunIdx = fonts.FindIndex(f => f.FamilyName == "SimSun");
        var arialIdx = fonts.FindIndex(f => f.FamilyName == "Arial");
        Assert.True(simsunIdx >= 0, "字体列表中应包含 SimSun");
        Assert.True(arialIdx >= 0, "字体列表中应包含 Arial");
        Assert.True(simsunIdx < arialIdx, "中文字体应排在西文字体之前");
    }
}
