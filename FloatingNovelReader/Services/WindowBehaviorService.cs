using System;
using System.Windows;
using System.Windows.Forms; // Screen
using FloatingNovelReader.Core;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 窗口行为：置顶、穿透、透明度、边缘吸附。
/// 桥接 WPF Window 与 Win32 API。
/// </summary>
public sealed class WindowBehaviorService
{
    private Window? _window;

    public ClickThroughState ClickThrough { get; private set; } = ClickThroughState.Normal;
    public TopmostState Topmost { get; private set; } = TopmostState.Topmost;

    public double AttachedOpacity => _window?.Opacity ?? 1.0;

    public event EventHandler? TopmostChanged;
    public event EventHandler? ClickThroughChanged;
    public event EventHandler? OpacityChanged;

    public void Attach(Window w)
    {
        _window = w;
    }

    public void Detach()
    {
        _window = null;
    }

    public void ToggleClickThrough()
    {
        if (_window == null) return;
        var enable = ClickThrough == ClickThroughState.Normal;
        ApplyClickThrough(enable);
    }

    public void ApplyClickThrough(bool enable)
    {
        if (_window == null) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;
        Win32Helper.SetClickThrough(hwnd, enable);
        ClickThrough = enable ? ClickThroughState.ClickThrough : ClickThroughState.Normal;
        EventBus.Default.Publish(Constants.EvtClickThroughChanged, ClickThrough);
        ClickThroughChanged?.Invoke(this, EventArgs.Empty);
        Log.Information("鼠标穿透: {State}", ClickThrough);
    }

    public void ToggleTopmost()
    {
        ApplyTopmost(Topmost != TopmostState.Topmost);
    }

    public void ApplyTopmost(bool topmost)
    {
        if (_window == null) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;
        Win32Helper.SetTopmost(hwnd, topmost);
        Topmost = topmost ? TopmostState.Topmost : TopmostState.Normal;
        EventBus.Default.Publish(Constants.EvtTopmostChanged, Topmost);
        TopmostChanged?.Invoke(this, EventArgs.Empty);
        Log.Information("置顶: {State}", Topmost);
    }

    public void SetOpacity(double opacity)
    {
        if (_window == null) return;
        var clamped = Math.Clamp(opacity, Constants.MinOpacity, Constants.MaxOpacity);
        _window.Opacity = clamped;
        EventBus.Default.Publish(Constants.EvtOpacityChanged, clamped);
        OpacityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void IncreaseOpacity()
    {
        if (_window == null) return;
        SetOpacity(_window.Opacity + Constants.OpacityStep);
    }

    public void DecreaseOpacity()
    {
        if (_window == null) return;
        SetOpacity(_window.Opacity - Constants.OpacityStep);
    }

    /// <summary>
    /// 边缘吸附：根据屏幕边界调整窗口位置，阈值由 Constants.EdgeSnapThreshold 控制。
    /// </summary>
    public void ApplyEdgeSnap(System.Windows.Point rawLeftTop)
    {
        if (_window == null) return;
        var v = System.Windows.Forms.Screen.FromHandle(
            new System.Windows.Interop.WindowInteropHelper(_window).Handle).WorkingArea;
        var threshold = Constants.EdgeSnapThreshold;

        double left = rawLeftTop.X;
        double top = rawLeftTop.Y;

        if (Math.Abs(left - v.Left) < threshold) left = v.Left;
        else if (Math.Abs((left + _window.Width) - v.Right) < threshold) left = v.Right - _window.Width;
        if (Math.Abs(top - v.Top) < threshold) top = v.Top;
        else if (Math.Abs((top + _window.Height) - v.Bottom) < threshold) top = v.Bottom - _window.Height;

        _window.Left = left;
        _window.Top = top;
    }
}
