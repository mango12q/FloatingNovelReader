using System;
using FloatingNovelReader.Services;
using FloatingNovelReader.ViewModels;
using Xunit;

namespace FloatingNovelReader.Tests.ViewModels;

/// <summary>
/// 朗读设置页 VM 的边界行为。
///
/// 注意：SettingsService 目前没有可注入的路径，会读取真实的 settings.json
/// （只读）。这些用例不断言初始值，只断言"改动后的范围与落点"，因此与文件内容无关，
/// 也不会写盘（不调用 Save）。
/// </summary>
public class TtsPanelViewModelTests
{
    private static TtsPanelViewModel NewPanel(out SettingsService settings)
    {
        settings = new SettingsService();
        return new TtsPanelViewModel(settings, new TtsVoiceCatalog(),
            new TtsService(new TtsEdgeClient(), new TtsPlayer(), settings));
    }

    [Theory]
    [InlineData(500, 100)]
    [InlineData(101, 100)]
    [InlineData(-999, -50)]
    [InlineData(0, 0)]
    [InlineData(35, 35)]
    public void RatePercent_IsClampedToProtocolRange(double input, double expected)
    {
        var panel = NewPanel(out var settings);

        panel.RatePercent = input;

        Assert.Equal(expected, panel.RatePercent);
        Assert.Equal(expected, settings.Current.Tts.RatePercent);
    }

    [Theory]
    [InlineData(500, 100)]
    [InlineData(-999, -50)]
    [InlineData(20, 20)]
    public void VolumePercent_IsClampedToProtocolRange(double input, double expected)
    {
        var panel = NewPanel(out var settings);

        panel.VolumePercent = input;

        Assert.Equal(expected, panel.VolumePercent);
        Assert.Equal(expected, settings.Current.Tts.VolumePercent);
    }

    [Fact]
    public void Voice_WritesThroughToSettings()
    {
        var panel = NewPanel(out var settings);

        panel.Voice = "zh-CN-YunxiNeural";

        Assert.Equal("zh-CN-YunxiNeural", settings.Current.Tts.Voice);
    }

    [Fact]
    public void Voice_NullBecomesEmptyString_NotEmptyNullReference()
    {
        var panel = NewPanel(out var settings);

        panel.Voice = null!;

        Assert.Equal(string.Empty, panel.Voice);
        Assert.Equal(string.Empty, settings.Current.Tts.Voice);
    }

    [Fact]
    public void AutoAdvancePage_WritesThroughToSettings()
    {
        var panel = NewPanel(out var settings);
        var target = !settings.Current.Tts.AutoAdvancePage;

        panel.AutoAdvancePage = target;

        Assert.Equal(target, settings.Current.Tts.AutoAdvancePage);
    }

    [Fact]
    public void Refresh_ReReadsFromSettings()
    {
        var panel = NewPanel(out var settings);
        panel.RatePercent = 42;

        // 模拟"取消"：Reload 换掉 Current，再 Refresh 让界面跟随
        settings.Reload();
        panel.Refresh();

        Assert.Equal(settings.Current.Tts.RatePercent, panel.RatePercent);
    }
}
