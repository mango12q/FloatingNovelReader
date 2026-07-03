using System.Collections.Generic;

namespace FloatingNovelReader.Models;

/// <summary>
/// 快捷键绑定配置。全局 + 模式覆盖两层。
/// </summary>
public sealed class HotkeyConfig
{
    /// <summary>全局快捷键：HotkeyAction 名 -> 按键字符串</summary>
    public Dictionary<string, string> GlobalHotkeys { get; set; } = new();

    /// <summary>模式内快捷键覆盖。key = 模式名，value = HotkeyAction -> 按键</summary>
    public Dictionary<string, Dictionary<string, string>> ModeOverrides { get; set; } = new();
}
