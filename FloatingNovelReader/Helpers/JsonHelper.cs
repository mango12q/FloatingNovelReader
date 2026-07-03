using System;
using System.IO;
using System.Text.Json;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// JSON 序列化/反序列化设置。
/// </summary>
public static class JsonHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings LoadSettings(string filePath)
    {
        if (!File.Exists(filePath))
            return CreateDefaultSettings();

        try
        {
            var json = File.ReadAllText(filePath);
            var s = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return s ?? CreateDefaultSettings();
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public static void SaveSettings(string filePath, AppSettings settings)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(filePath, json);
    }

    public static AppSettings CreateDefaultSettings()
    {
        var s = new AppSettings();
        // 默认全局快捷键
        s.Hotkeys.GlobalHotkeys["NextPage"] = "Space";
        s.Hotkeys.GlobalHotkeys["PrevPage"] = "Back";
        s.Hotkeys.GlobalHotkeys["NextChapter"] = "Next";   // PageDown
        s.Hotkeys.GlobalHotkeys["PrevChapter"] = "Prior";  // PageUp
        s.Hotkeys.GlobalHotkeys["IncreaseOpacity"] = "Add";
        s.Hotkeys.GlobalHotkeys["DecreaseOpacity"] = "Subtract";
        s.Hotkeys.GlobalHotkeys["ToggleClickThrough"] = "F3";
        s.Hotkeys.GlobalHotkeys["ToggleTopmost"] = "F4";
        s.Hotkeys.GlobalHotkeys["ToggleAutoRead"] = "F5";
        s.Hotkeys.GlobalHotkeys["AutoReadFaster"] = "F6";
        s.Hotkeys.GlobalHotkeys["AutoReadSlower"] = "F7";
        s.Hotkeys.GlobalHotkeys["HideWindow"] = "F8";
        s.Hotkeys.GlobalHotkeys["ShowChapterList"] = "F9";
        s.Hotkeys.GlobalHotkeys["AddBookmark"] = "F10";
        s.Hotkeys.GlobalHotkeys["TogglePause"] = "F11";
        s.Hotkeys.GlobalHotkeys["ShowBookmarkList"] = "F12";

        // 自动阅读模式下的覆盖
        s.Hotkeys.ModeOverrides["autoRead"] = new System.Collections.Generic.Dictionary<string, string>
        {
            ["NextPage"] = "Space",
            ["PrevPage"] = "Back",
        };
        return s;
    }
}
