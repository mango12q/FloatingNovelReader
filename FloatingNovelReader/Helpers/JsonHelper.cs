using System;
using System.IO;
using System.Text.Json;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// JSON 序列化/反序列化设置。
/// </summary>
public static class JsonHelper
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 容忍键名大小写差异。缺省时 System.Text.Json 按命名策略做大小写敏感匹配，
        // 手工把键写成 PascalCase（如 "FontSize"）会被静默忽略、退回默认值。
        PropertyNameCaseInsensitive = true,
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
        catch (Exception ex)
        {
            // 以前这里是空 catch：解析失败就静默返回默认设置，
            // 用户会"毫无提示地丢掉全部个性化配置"，而且随后一次 Save 就把坏文件彻底覆盖。
            // 现在：记日志 + 备份原文件，让配置还有人工找回的机会。
            Log.Error(ex, "读取设置失败，已回退默认设置: {Path}", filePath);
            TryBackupCorruptFile(filePath);
            return CreateDefaultSettings();
        }
    }

    /// <summary>解析失败时把原文件另存一份，避免被后续 Save 覆盖。</summary>
    private static void TryBackupCorruptFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var backup = filePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
            File.Copy(filePath, backup, overwrite: true);
            Log.Warning("无法解析的设置文件已备份到 {Backup}", backup);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "备份设置文件失败 (忽略)");
        }
    }

    public static void SaveSettings(string filePath, AppSettings settings)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, Options);

        // 原子写：先写同目录临时文件，再改名替换。
        // 直接用 File.WriteAllText 时，若进程被杀 / 断电，会留下半截 JSON，
        // 下次启动解析失败 → 用户的全部配置被回退成默认值。
        var tmp = filePath + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, filePath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch { /* 临时文件清理失败不影响结果 */ }
        }
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

        // 朗读相关：默认**不绑定**（空值）。
        // 给它们默认全局组合键会在所有程序里抢占按键；这里只保证它们在
        // "设置 → 快捷键" 列表里可见、用户想绑就能绑。
        s.Hotkeys.GlobalHotkeys["SpeakFromHere"] = string.Empty;
        s.Hotkeys.GlobalHotkeys["SpeakFromHereMinutes"] = string.Empty;
        s.Hotkeys.GlobalHotkeys["SpeakFromHereChapters"] = string.Empty;
        s.Hotkeys.GlobalHotkeys["StopSpeaking"] = string.Empty;

        // 自动阅读模式下的覆盖
        s.Hotkeys.ModeOverrides["autoRead"] = new System.Collections.Generic.Dictionary<string, string>
        {
            ["NextPage"] = "Space",
            ["PrevPage"] = "Back",
        };
        return s;
    }
}
