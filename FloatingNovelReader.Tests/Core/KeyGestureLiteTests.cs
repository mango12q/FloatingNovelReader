using System.Windows.Input;
using FloatingNovelReader.Core;
using Xunit;

namespace FloatingNovelReader.Tests.Core;

/// <summary>
/// KeyGestureLite 单元测试。
/// 重点验证: 单键 (无修饰键) 能正确解析、往返 ToString/TryParse。
/// </summary>
public class KeyGestureLiteTests
{
    [Theory]
    [InlineData("N", System.Windows.Input.Key.N, ModifierKeys.None)]
    [InlineData("Down", System.Windows.Input.Key.Down, ModifierKeys.None)]
    [InlineData("Space", System.Windows.Input.Key.Space, ModifierKeys.None)]
    [InlineData("F1", System.Windows.Input.Key.F1, ModifierKeys.None)]
    [InlineData("PageDown", System.Windows.Input.Key.PageDown, ModifierKeys.None)]
    [InlineData("Ctrl+N", System.Windows.Input.Key.N, ModifierKeys.Control)]
    [InlineData("Ctrl+Shift+F5", System.Windows.Input.Key.F5, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData("Alt+Down", System.Windows.Input.Key.Down, ModifierKeys.Alt)]
    public void TryParse_ValidStrings_ReturnsExpectedGesture(string input, System.Windows.Input.Key expectedKey, ModifierKeys expectedMods)
    {
        var ok = KeyGestureLite.TryParse(input, out var g);
        Assert.True(ok);
        Assert.Equal(expectedKey, g.Key);
        Assert.Equal(expectedMods, g.Modifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+")]
    [InlineData("NotAKey")]
    public void TryParse_InvalidStrings_ReturnsFalse(string input)
    {
        var ok = KeyGestureLite.TryParse(input, out _);
        Assert.False(ok);
    }

    [Fact]
    public void ToString_SingleKey_NoPlusSign()
    {
        // 单键不应输出 "N+" 这种尾随加号
        var g = new KeyGestureLite(System.Windows.Input.Key.N, ModifierKeys.None);
        Assert.Equal("N", g.ToString());
    }

    [Theory]
    [InlineData(System.Windows.Input.Key.N, ModifierKeys.None, "N")]
    [InlineData(System.Windows.Input.Key.Down, ModifierKeys.None, "Down")]
    [InlineData(System.Windows.Input.Key.F1, ModifierKeys.None, "F1")]
    [InlineData(System.Windows.Input.Key.N, ModifierKeys.Control, "Ctrl+N")]
    [InlineData(System.Windows.Input.Key.F5, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+F5")]
    [InlineData(System.Windows.Input.Key.Left, ModifierKeys.Alt, "Alt+Left")]
    public void ToString_RoundTrip(System.Windows.Input.Key key, ModifierKeys mods, string expected)
    {
        var g = new KeyGestureLite(key, mods);
        Assert.Equal(expected, g.ToString());
    }
}
