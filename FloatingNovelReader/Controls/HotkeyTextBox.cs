using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FloatingNovelReader.Core;

namespace FloatingNovelReader.Controls;

/// <summary>
/// 快捷键录入控件。
/// 行为:
///   - 单击 / 获得焦点: 进入「录制」状态, 显示 "[ 按下快捷键... ]"
///   - 按下组合键: 完成录制, 写入 Hotkey 依赖属性 (支持单键: N / Down / F1 等)
///   - ESC: 取消录制, 恢复原始 Hotkey 值
///   - Backspace/Delete: 清空 Hotkey (视为显式解除绑定)
///   - 右键: 清空 Hotkey (等同于禁用该快捷键, 与 Backspace/Delete 行为一致)
///   - 同一组合键按两次: 第二次视为取消 (恢复原值), 避免误改
///   - 失去焦点: 退出录制, 恢复显示
///   - 录制期间全局热键被屏蔽, 避免刚录入的"N"被当作"下一页"立刻触发
/// </summary>
public sealed class HotkeyTextBox : TextBox
{
    /// <summary>未设置快捷键时的占位显示文本</summary>
    public const string EmptyDisplayText = "[ 未设置 - 右键清空 ]";
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(string), typeof(HotkeyTextBox), new FrameworkPropertyMetadata("",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public string Hotkey
    {
        get => (string)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    // 录制状态
    private string? _originalHotkey;        // 进入录制前的值, ESC 恢复
    private string? _lastRecordedGesture;   // 上一次录入的组合键字符串, 用于"再按一次取消"
    private bool _isRecording;
    // 录制期间持有的 IDisposable, Dispose 时释放全局热键屏蔽计数器
    // 必须用字段持有, 避免 BeginRecording 返回的 token 被 GC 立即回收
    private IDisposable? _recordingToken;

    public HotkeyTextBox()
    {
        IsReadOnly = true;
        IsTabStop = true;
        Focusable = true;
        Cursor = Cursors.Hand;

        GotFocus += OnGotFocus;
        LostFocus += OnLostFocus;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox tb)
        {
            // Hotkey 外部更新时, 同步到 TextBox 文本, 并清空录制中间态
            if (!tb._isRecording)
                tb.RefreshDisplay();
        }
    }

    /// <summary>根据当前 Hotkey 属性刷新 TextBox 显示。非录制态时使用。</summary>
    private void RefreshDisplay()
    {
        Text = string.IsNullOrEmpty(Hotkey) ? EmptyDisplayText : Hotkey;
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        BeginRecording();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        // 失去焦点 → 退出录制, 显示当前值 (不修改 Hotkey)
        EndRecording();
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 第一次点击: 直接进入录制, 不让 TextBox 自身做默认处理
        if (!_isRecording)
        {
            BeginRecording();
        }
    }

    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 右键: 清空当前快捷键 (禁用此快捷键)
        // - 若在录制中: 保持录制状态, 但把 Hotkey 清空并显示占位符, 允许用户继续录新键
        // - 若不在录制: 清空并 UpdateSource
        Hotkey = "";
        _lastRecordedGesture = null;
        RefreshDisplay();
        // 立即把清空写回 ViewModel, 避免依赖 LostFocus 时机
        var binding = (BindingExpressionBase?)GetBindingExpression(HotkeyProperty);
        binding?.UpdateSource();
        e.Handled = true; // 阻止系统右键菜单弹出
    }

    private void BeginRecording()
    {
        if (_isRecording) return;
        _isRecording = true;
        _originalHotkey = Hotkey;
        _lastRecordedGesture = null;
        Text = "[ 按下快捷键... Esc 取消 ]";
        // 通知全局热键: 我正在录制, 请暂时放行按键, 避免刚录入的"N"被当作"下一页"立刻触发
        // 计数器 (而非 bool) 让多个 HotkeyTextBox 嵌套录制时不会误关屏蔽
        _recordingToken = HotkeyManager.PushRecording();
    }

    private void EndRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;
        _originalHotkey = null;
        _lastRecordedGesture = null;
        RefreshDisplay();
        // Dispose 释放计数器, 全局热键恢复
        _recordingToken?.Dispose();
        _recordingToken = null;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecording) return;

        e.Handled = true;
        var key = e.Key == Key.ImeProcessed ? Key.None : e.Key;

        // ESC → 恢复原值并退出录制
        if (key == Key.Escape)
        {
            if (_originalHotkey != null) Hotkey = _originalHotkey;
            _isRecording = false;
            // 不再调用 EndRecording (避免 _originalHotkey 清空后的副作用)
            _originalHotkey = null;
            _lastRecordedGesture = null;
            _recordingToken?.Dispose();
            _recordingToken = null;
            RefreshDisplay();
            // 把焦点移走以触发 LostFocus 显示
            MoveFocusAway();
            return;
        }

        // Backspace/Delete → 显式清空
        if (key == Key.Back || key == Key.Delete)
        {
            Hotkey = "";
            // 保持录制状态, 显示"未设置"占位符, 用户可继续录新键
            RefreshDisplay();
            _lastRecordedGesture = null;
            // 把清空也写回 ViewModel
            var binding = (BindingExpressionBase?)GetBindingExpression(HotkeyProperty);
            binding?.UpdateSource();
            return;
        }

        // 修饰键单独按下不录入
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt  || key == Key.RightAlt  ||
            key == Key.LeftShift|| key == Key.RightShift||
            key == Key.LWin     || key == Key.RWin)
        {
            return;
        }

        // 允许任意单键或组合键 (包括纯字母 N / 方向键 Down / F1 / Ctrl+N 等)
        // 仅屏蔽 Tab / System 等控制键 (避免意外触发系统命令或焦点切换)
        if (key == Key.Tab || key == Key.System || key == Key.NoName)
        {
            return;
        }

        var mods = Keyboard.Modifiers;
        var gesture = new KeyGestureLite(key, mods).ToString();

        // 按下与上次相同的组合 → 取消 (恢复原值)
        if (!string.IsNullOrEmpty(_lastRecordedGesture) && _lastRecordedGesture == gesture)
        {
            if (_originalHotkey != null) Hotkey = _originalHotkey;
            _isRecording = false;
            _originalHotkey = null;
            _lastRecordedGesture = null;
            _recordingToken?.Dispose();
            _recordingToken = null;
            RefreshDisplay();
            MoveFocusAway();
            return;
        }

        // 正常录入
        Hotkey = gesture;
        Text = gesture;
        _lastRecordedGesture = gesture;

        // 写入 ViewModel
        var bind = (BindingExpressionBase?)GetBindingExpression(HotkeyProperty);
        bind?.UpdateSource();
    }

    private void MoveFocusAway()
    {
        // 让焦点移到下一个控件, 触发 LostFocus 走显示逻辑
        var scope = FocusManager.GetFocusScope(this);
        if (scope != null)
        {
            var next = (FrameworkElement?)FocusManager.GetFocusedElement(scope);
            // 使用 KeyboardNavigation 找下一个 tab stop
            this.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }
}
