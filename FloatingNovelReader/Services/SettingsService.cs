using System;
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
    }
}
