using System;
using System.Collections.Generic;
using FloatingNovelReader.Core;

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

    /// <summary>
    /// 补全所有 <see cref="HotkeyAction"/> 的条目（值留空 = 未绑定）。
    ///
    /// 快捷键是按枚举**名字**存的，所以新增枚举成员不会破坏旧配置；
    /// 但不回填的话，老用户升级后在"设置 → 快捷键"里根本看不到新动作，
    /// 等于新功能无处可配。已有绑定不会被覆盖。
    /// </summary>
    public void EnsureAllActions()
    {
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var name = action.ToString();
            if (!GlobalHotkeys.ContainsKey(name))
                GlobalHotkeys[name] = string.Empty;
        }
    }
}
