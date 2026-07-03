using System;
using System.Collections.Generic;
using System.Windows.Input;
using Gma.System.MouseKeyHook;
using Serilog;

namespace FloatingNovelReader.Core;

/// <summary>
/// 全局快捷键动作枚举。所有可被绑定的动作都集中在此。
/// </summary>
public enum HotkeyAction
{
    NextPage,
    PrevPage,
    NextChapter,
    PrevChapter,
    IncreaseOpacity,
    DecreaseOpacity,
    ToggleClickThrough,
    ToggleTopmost,
    ToggleAutoRead,
    AutoReadFaster,
    AutoReadSlower,
    HideWindow,
    ShowChapterList,
    AddBookmark,
    ShowBookmarkList,
    TogglePause,
}

/// <summary>
/// 一个按键组合的简单表示。"Ctrl+Shift+F5" 这样的字符串解析为各修饰键 + 主键。
/// </summary>
public readonly record struct KeyGestureLite(
    Key Key,
    ModifierKeys Modifiers)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public static bool TryParse(string s, out KeyGestureLite gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var mods = ModifierKeys.None;
        Key? key = null;
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || t.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Control;
            else if (t.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Shift;
            else if (t.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Alt;
            else if (t.Equals("Win", StringComparison.OrdinalIgnoreCase) || t.Equals("Meta", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Windows;
            else if (Enum.TryParse<Key>(t, true, out var k))
                key = k;
        }
        if (key is null) return false;
        gesture = new KeyGestureLite(key.Value, mods);
        return true;
    }
}

/// <summary>
/// 全局快捷键管理器。封装 MouseKeyHook 并提供：
/// 1. 全局热键 -> HotkeyAction 映射
/// 2. 150ms 防抖
/// 3. 模式覆盖（自动阅读 / 手动阅读模式下的不同行为）
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private IKeyboardMouseEvents? _hook;
    private readonly Dictionary<KeyGestureLite, HotkeyAction> _globalBindings = new();
    private readonly Dictionary<KeyGestureLite, HotkeyAction> _modeBindings = new();
    private readonly Dictionary<HotkeyAction, DateTime> _lastFireTime = new();
    private readonly object _lock = new();
    private bool _isAutoReadMode;
    private int _suppressDepth; // 用于在录制快捷键时临时屏蔽全局触发

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public bool IsAutoReadMode
    {
        get => _isAutoReadMode;
        set => _isAutoReadMode = value;
    }

    /// <summary>
    /// 在录制快捷键期间临时屏蔽全局触发。
    /// 用 using 配对可保证退出 using 时一定还原，避免异常导致永远屏蔽。
    /// </summary>
    public IDisposable Suppress()
    {
        System.Threading.Interlocked.Increment(ref _suppressDepth);
        return new Restore(() => System.Threading.Interlocked.Decrement(ref _suppressDepth));
    }

    /// <summary>
    /// 是否有 HotkeyTextBox 处于录制状态。全局钩子看到此标志后将直接放行按键，
    /// 避免正在录入的单键 (例如 N) 被全局热键立刻当作"下一页"触发。
    /// 用计数器避免多个 HotkeyTextBox 嵌套录制时误关屏蔽。
    /// </summary>
    private static int _recordingCount;
    public static bool IsAnyHotkeyRecording => System.Threading.Volatile.Read(ref _recordingCount) > 0;

    public static IDisposable PushRecording()
    {
        System.Threading.Interlocked.Increment(ref _recordingCount);
        return new Restore(() => System.Threading.Interlocked.Decrement(ref _recordingCount));
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;
        public Restore(Action onDispose) { _onDispose = onDispose; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }

    public void Start()
    {
        if (_hook != null) return;
        _hook = Hook.GlobalEvents();
        _hook.KeyDown += OnKeyDown;
        _hook.KeyUp += OnKeyUp;
        Log.Information("全局钩子已启动");
    }

    public void Stop()
    {
        if (_hook == null) return;
        _hook.KeyDown -= OnKeyDown;
        _hook.KeyUp -= OnKeyUp;
        _hook.Dispose();
        _hook = null;
        Log.Information("全局钩子已停止");
    }

    public void SetGlobalBindings(IDictionary<string, string> bindings)
    {
        lock (_lock)
        {
            _globalBindings.Clear();
            foreach (var kv in bindings)
            {
                if (!Enum.TryParse<HotkeyAction>(kv.Key, out var action)) continue;
                if (!KeyGestureLite.TryParse(kv.Value, out var g)) continue;
                _globalBindings[g] = action;
            }
        }
    }

    public void SetModeOverride(string modeName, IDictionary<string, string> bindings)
    {
        lock (_lock)
        {
            if (modeName == "autoRead" && _isAutoReadMode)
            {
                _modeBindings.Clear();
                foreach (var kv in bindings)
                {
                    if (!Enum.TryParse<HotkeyAction>(kv.Key, out var action)) continue;
                    if (!KeyGestureLite.TryParse(kv.Value, out var g)) continue;
                    _modeBindings[g] = action;
                }
            }
        }
    }

    public void RefreshModeBindings()
    {
        // 当 IsAutoReadMode 变化后由外部调用，按需重新构建 _modeBindings
    }

    private void OnKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        // 录制快捷键期间: 不让全局热键触发, 避免刚录入的组合键立刻翻页
        if (System.Threading.Volatile.Read(ref _suppressDepth) > 0) return;
        // 任意 HotkeyTextBox 正在录制时屏蔽全局热键
        if (IsAnyHotkeyRecording) return;

        // 把 System.Windows.Forms.Key 转换为 System.Windows.Input.Key
        if (!Enum.TryParse<System.Windows.Input.Key>(e.KeyCode.ToString(), true, out var wpfKey))
        {
            // 字母数字键通常可以直接转换
            if (e.KeyCode >= System.Windows.Forms.Keys.A && e.KeyCode <= System.Windows.Forms.Keys.Z)
                wpfKey = (System.Windows.Input.Key)((int)System.Windows.Input.Key.A + (e.KeyCode - System.Windows.Forms.Keys.A));
            else
                return;
        }
        var mods = System.Windows.Input.Keyboard.Modifiers;
        var gesture = new KeyGestureLite(wpfKey, mods);

        HotkeyAction? action = null;

        // 模式层优先
        lock (_lock)
        {
            if (_modeBindings.TryGetValue(gesture, out var a))
                action = a;
            else if (_globalBindings.TryGetValue(gesture, out a))
                action = a;
        }

        if (action is null) return;

        // 防抖
        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (_lastFireTime.TryGetValue(action.Value, out var last) &&
                (now - last).TotalMilliseconds < Constants.HotkeyDebounceMs)
                return;
            _lastFireTime[action.Value] = now;
        }

        Log.Debug("全局快捷键命中: {Gesture} -> {Action}", gesture, action);
        try
        {
            HotkeyPressed?.Invoke(this, action.Value);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理快捷键 {Action} 时异常", action);
        }
    }

    private void OnKeyUp(object? sender, System.Windows.Forms.KeyEventArgs e) { /* no-op */ }

    public void Dispose() => Stop();
}
