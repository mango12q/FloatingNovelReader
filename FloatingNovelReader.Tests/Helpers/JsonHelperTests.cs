using System;
using System.IO;
using FloatingNovelReader.Helpers;
using Xunit;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// settings.json 的读写健壮性。
/// 对应体检报告 P0「2.2 缺字段/坏文件 → 全部设置被静默回退」与 P1「2.5 非原子写」。
/// </summary>
public class JsonHelperTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public JsonHelperTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"fnr_json_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    [Fact]
    public void LoadSettings_MissingFile_ReturnsDefaultsWithDefaultHotkeys()
    {
        var s = JsonHelper.LoadSettings(_file);
        Assert.NotNull(s);
        Assert.True(s.Hotkeys.GlobalHotkeys.ContainsKey("NextPage"));
        Assert.Equal(18, s.Display.FontSize);
    }

    [Fact]
    public void SaveSettings_ThenLoad_RoundTrips()
    {
        var s = JsonHelper.CreateDefaultSettings();
        s.Display.FontSize = 24;
        s.AutoReadIntervalSec = 7;
        JsonHelper.SaveSettings(_file, s);

        var loaded = JsonHelper.LoadSettings(_file);
        Assert.Equal(24, loaded.Display.FontSize);
        Assert.Equal(7, loaded.AutoReadIntervalSec);
    }

    [Fact]
    public void SaveSettings_IsAtomicAndLeavesNoTempFile()
    {
        JsonHelper.SaveSettings(_file, JsonHelper.CreateDefaultSettings());

        Assert.True(File.Exists(_file));
        // 原子写用的同目录临时文件必须被清理干净
        Assert.False(File.Exists(_file + ".tmp"));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void LoadSettings_MissingOptionalFields_DoesNotThrow()
    {
        // 旧版本 settings.json 缺字段：不应抛异常，缺的字段取类型默认值
        File.WriteAllText(_file, "{\"autoReadIntervalSec\":7}");
        var s = JsonHelper.LoadSettings(_file);
        Assert.Equal(7, s.AutoReadIntervalSec);
        Assert.Equal(18, s.Display.FontSize);
    }

    [Fact]
    public void LoadSettings_PascalCaseKeys_AreHonoured()
    {
        // 缺 PropertyNameCaseInsensitive 时，手工写成 PascalCase 的键会被静默忽略
        File.WriteAllText(_file, "{\"AutoReadIntervalSec\":7,\"Display\":{\"FontSize\":24}}");
        var s = JsonHelper.LoadSettings(_file);
        Assert.Equal(7, s.AutoReadIntervalSec);
        Assert.Equal(24, s.Display.FontSize);
    }

    [Fact]
    public void LoadSettings_CorruptFile_ReturnsDefaultsAndBacksUpOriginal()
    {
        // 类型不匹配才会真的抛 JsonException；此时不能静默丢掉用户全部配置
        File.WriteAllText(_file, "{\"autoReadIntervalSec\":\"七秒\"}");

        var s = JsonHelper.LoadSettings(_file);

        Assert.NotNull(s);
        Assert.Equal(18, s.Display.FontSize);
        // 原文件必须被备份，否则随后一次 Save 就把用户配置彻底覆盖了
        Assert.Single(Directory.GetFiles(_dir, "settings.json.corrupt-*.json"));
        Assert.True(File.Exists(_file));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
