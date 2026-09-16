using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Xunit;

namespace FloatingNovelReader.Tests.Models;

/// <summary>
/// 快捷键配置的回填。
///
/// 对应我在审计里指出的问题：新增 <see cref="HotkeyAction"/> 成员后，
/// 老用户的 settings.json 里没有这两个键，导致它们在"设置 → 快捷键"里**不可见**，
/// 新功能没有地方可配置。
/// </summary>
public class HotkeyConfigTests
{
    [Fact]
    public void EnsureAllActions_AddsEveryActionAsUnbound()
    {
        var config = new HotkeyConfig();   // 全新配置，一条绑定都没有

        config.EnsureAllActions();

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            Assert.True(config.GlobalHotkeys.ContainsKey(action.ToString()),
                $"缺少动作 {action}");
        }
        // 新补的条目必须是"未绑定"（空串），而不是被塞进某个默认热键
        Assert.All(config.GlobalHotkeys.Values, v => Assert.Equal(string.Empty, v));
    }

    [Fact]
    public void EnsureAllActions_DoesNotOverwriteExistingBindings()
    {
        var config = new HotkeyConfig
        {
            GlobalHotkeys = new Dictionary<string, string>
            {
                ["NextPage"] = "Space",
                ["SpeakFromHere"] = "Ctrl+Alt+S",
            },
        };

        config.EnsureAllActions();

        Assert.Equal("Space", config.GlobalHotkeys["NextPage"]);
        Assert.Equal("Ctrl+Alt+S", config.GlobalHotkeys["SpeakFromHere"]);
        // 其余的仍被补进来
        Assert.Equal(string.Empty, config.GlobalHotkeys["StopSpeaking"]);
    }

    [Fact]
    public void EnsureAllActions_IsIdempotent()
    {
        var config = new HotkeyConfig { GlobalHotkeys = { ["NextPage"] = "Space" } };

        config.EnsureAllActions();
        var first = config.GlobalHotkeys.Count;
        config.EnsureAllActions();

        Assert.Equal(first, config.GlobalHotkeys.Count);
        Assert.Equal("Space", config.GlobalHotkeys["NextPage"]);
    }

    [Fact]
    public void DefaultSettings_IncludeTheTtsActions()
    {
        // 新用户的默认配置里要有朗读这几项，否则同样不可见。
        // 三个入口（从当前开始 / N 分钟 / N 章）都要在列，才能各自单独绑键。
        var defaults = FloatingNovelReader.Helpers.JsonHelper.CreateDefaultSettings();

        Assert.True(defaults.Hotkeys.GlobalHotkeys.ContainsKey("SpeakFromHere"));
        Assert.True(defaults.Hotkeys.GlobalHotkeys.ContainsKey("SpeakFromHereMinutes"));
        Assert.True(defaults.Hotkeys.GlobalHotkeys.ContainsKey("SpeakFromHereChapters"));
        Assert.True(defaults.Hotkeys.GlobalHotkeys.ContainsKey("StopSpeaking"));

        // 默认必须是**未绑定**：给它们默认全局键会在所有程序里抢按键
        Assert.Equal(string.Empty, defaults.Hotkeys.GlobalHotkeys["SpeakFromHere"]);
        Assert.Equal(string.Empty, defaults.Hotkeys.GlobalHotkeys["SpeakFromHereMinutes"]);
        Assert.Equal(string.Empty, defaults.Hotkeys.GlobalHotkeys["SpeakFromHereChapters"]);
        Assert.Equal(string.Empty, defaults.Hotkeys.GlobalHotkeys["StopSpeaking"]);
    }

    [Fact]
    public void TtsHotkeyActions_AreDistinctAndParseable()
    {
        // 三个入口必须是三个独立动作：共用一个动作就没法分别绑键，
        // 也没法在 ReaderViewModel 里路由到不同的停止条件。
        Assert.NotEqual(HotkeyAction.SpeakFromHere, HotkeyAction.SpeakFromHereMinutes);
        Assert.NotEqual(HotkeyAction.SpeakFromHere, HotkeyAction.SpeakFromHereChapters);
        Assert.NotEqual(HotkeyAction.SpeakFromHereMinutes, HotkeyAction.SpeakFromHereChapters);

        foreach (var name in new[] { "SpeakFromHere", "SpeakFromHereMinutes", "SpeakFromHereChapters", "StopSpeaking" })
        {
            Assert.True(Enum.TryParse<HotkeyAction>(name, out var parsed), $"{name} 解析失败");
            // 按名字存取：ToString 必须能原样还原
            Assert.Equal(name, parsed.ToString());
        }
    }

    [Fact]
    public void EveryActionNameRoundTripsThroughEnumParse()
    {
        // SetGlobalBindings 用 Enum.TryParse<HotkeyAction>(kv.Key) 解析键名：
        // 名字一旦对不上，绑定会被静默丢弃
        var config = new HotkeyConfig();
        config.EnsureAllActions();

        foreach (var key in config.GlobalHotkeys.Keys)
        {
            Assert.True(Enum.TryParse<HotkeyAction>(key, out _), $"键名 {key} 无法解析回 HotkeyAction");
        }
    }

    [Fact]
    public void SettingsWindow_HasAChineseDisplayNameForEveryAction()
    {
        // SettingsWindow.DisplayNameOf 是硬编码 switch：漏掉新动作时，设置页会显示
        // 英文枚举名（"SpeakFromHereMinutes"），用户根本不知道该绑哪个。
        var method = typeof(FloatingNovelReader.Views.SettingsWindow).GetMethod(
            "DisplayNameOf", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var missing = new List<string>();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var name = action.ToString();
            var display = (string)method!.Invoke(null, new object[] { name })!;
            if (display == name) missing.Add(name);
        }

        Assert.True(missing.Count == 0,
            "设置页缺少这些快捷键的中文名（会直接把英文枚举名显示给用户）：" +
            string.Join(", ", missing));
    }
}
