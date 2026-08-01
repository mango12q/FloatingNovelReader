using System;
using System.IO;
using System.Text.Json;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 设置读写。从 settings.json 加载，启动时初始化，运行时通过事件通知。
/// </summary>
public sealed class SettingsService
{
    private AppSettings _settings;
    private readonly string _filePath;

    public AppSettings Current => _settings;

    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        _filePath = Constants.SettingsFile;
        _settings = Helpers.JsonHelper.LoadSettings(_filePath);
    }

    public void Save()
    {
        try
        {
            Helpers.JsonHelper.SaveSettings(_filePath, _settings);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            Core.SelfInstaller.SetAutoStart(_settings.AutoStart);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存设置失败");
        }
    }

    public void Reload()
    {
        _settings = Helpers.JsonHelper.LoadSettings(_filePath);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        Core.SelfInstaller.SetAutoStart(_settings.AutoStart);
    }

    public void Reset()
    {
        _settings = new AppSettings();
        _settings.Hotkeys = Helpers.JsonHelper.CreateDefaultSettings().Hotkeys;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 导出设置到指定目录（包含 settings.json + library.db）。
    /// </summary>
    public string ExportSettings(string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        var settingsPath = Path.Combine(targetDir, "settings.json");
        Helpers.JsonHelper.SaveSettings(settingsPath, _settings);

        var dbSource = Constants.DbFile;
        if (File.Exists(dbSource))
        {
            var dbTarget = Path.Combine(targetDir, "library.db");
            File.Copy(dbSource, dbTarget, overwrite: true);
        }

        return settingsPath;
    }

    /// <summary>
    /// 从指定目录导入设置（合并 settings.json）。
    /// </summary>
    public void ImportSettings(string sourceDir)
    {
        var settingsPath = Path.Combine(sourceDir, "settings.json");
        if (!File.Exists(settingsPath))
            throw new FileNotFoundException("未找到 settings.json", settingsPath);

        var json = File.ReadAllText(settingsPath);
        var imported = JsonSerializer.Deserialize<AppSettings>(json, Helpers.JsonHelper.Options);
        if (imported == null)
            throw new InvalidOperationException("设置文件格式无效");

        MergeSettings(imported);
        Save();
    }

    /// <summary>
    /// 将 imported 的非默认值合并到当前 _settings 中。
    /// </summary>
    private void MergeSettings(AppSettings imported)
    {
        _settings.StartupBehavior = imported.StartupBehavior;
        _settings.HotkeyMode = imported.HotkeyMode;
        _settings.AutoStart = imported.AutoStart;
        _settings.Hotkeys = imported.Hotkeys;
        _settings.Display = imported.Display;
        _settings.AutoReadIntervalSec = imported.AutoReadIntervalSec;
        _settings.DefaultWidth = imported.DefaultWidth;
        _settings.DefaultHeight = imported.DefaultHeight;
        _settings.MinWidth = imported.MinWidth;
        _settings.MinHeight = imported.MinHeight;
        _settings.EdgeSnapThreshold = imported.EdgeSnapThreshold;
        _settings.ControlBarShowDelayMs = imported.ControlBarShowDelayMs;
        _settings.ControlBarHideDelayMs = imported.ControlBarHideDelayMs;
    }
}
