using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FloatingNovelReader.Core;

namespace FloatingNovelReader.Controls;

/// <summary>
/// 8 个拖拽方向的辅助控件。挂载在窗口四边 / 四角。
/// </summary>
public sealed class ResizeGrip : Control
{
    public enum GripDirection { Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction), typeof(GripDirection), typeof(ResizeGrip), new PropertyMetadata(GripDirection.Right));

    public GripDirection Direction
    {
        get => (GripDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public static readonly DependencyProperty TargetWindowProperty = DependencyProperty.Register(
        nameof(TargetWindow), typeof(Window), typeof(ResizeGrip), new PropertyMetadata(null));

    public Window? TargetWindow
    {
        get => (Window?)GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    static ResizeGrip()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizeGrip), new FrameworkPropertyMetadata(typeof(ResizeGrip)));
    }

    public ResizeGrip()
    {
        Background = Brushes.Transparent;
        IsHitTestVisible = true;
        Loaded += (s, e) => UpdateCursor();
        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseDown;
    }

    private void UpdateCursor()
    {
        Cursor = Direction switch
        {
            GripDirection.Left or GripDirection.Right => Cursors.SizeWE,
            GripDirection.Top or GripDirection.Bottom => Cursors.SizeNS,
            GripDirection.TopLeft or GripDirection.BottomRight => Cursors.SizeNWSE,
            GripDirection.TopRight or GripDirection.BottomLeft => Cursors.SizeNESW,
            _ => Cursors.Arrow
        };
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (TargetWindow == null) return;
        DragMove();
    }

    private Point _startPos;
    private System.Windows.Size _startSize;
    private System.Windows.Point _startLoc;

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // 简化版：交给父级 ResizableBorder 统一处理缩放
    }

    private void DragMove()
    {
        if (TargetWindow == null) return;
        _startPos = System.Windows.Forms.Control.MousePosition.ToWpfPoint();
        _startSize = new System.Windows.Size(TargetWindow.Width, TargetWindow.Height);
        _startLoc = new System.Windows.Point(TargetWindow.Left, TargetWindow.Top);

        CaptureMouse();
        MouseMove -= OnMouseMoveDuringDrag;
        MouseMove += OnMouseMoveDuringDrag;
        MouseLeftButtonUp += OnMouseUp;
    }

    private void OnMouseMoveDuringDrag(object sender, MouseEventArgs e)
    {
        if (TargetWindow == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var cur = System.Windows.Forms.Control.MousePosition.ToWpfPoint();
        var dx = cur.X - _startPos.X;
        var dy = cur.Y - _startPos.Y;

        double newLeft = _startLoc.X;
        double newTop = _startLoc.Y;
        double newWidth = _startSize.Width;
        double newHeight = _startSize.Height;

        switch (Direction)
        {
            case GripDirection.Left:
                newLeft = _startLoc.X + dx;
                newWidth = _startSize.Width - dx;
                break;
            case GripDirection.Right:
                newWidth = _startSize.Width + dx;
                break;
            case GripDirection.Top:
                newTop = _startLoc.Y + dy;
                newHeight = _startSize.Height - dy;
                break;
            case GripDirection.Bottom:
                newHeight = _startSize.Height + dy;
                break;
            case GripDirection.TopLeft:
                newLeft = _startLoc.X + dx;
                newTop = _startLoc.Y + dy;
                newWidth = _startSize.Width - dx;
                newHeight = _startSize.Height - dy;
                break;
            case GripDirection.TopRight:
                newTop = _startLoc.Y + dy;
                newWidth = _startSize.Width + dx;
                newHeight = _startSize.Height - dy;
                break;
            case GripDirection.BottomLeft:
                newLeft = _startLoc.X + dx;
                newWidth = _startSize.Width - dx;
                newHeight = _startSize.Height + dy;
                break;
            case GripDirection.BottomRight:
                newWidth = _startSize.Width + dx;
                newHeight = _startSize.Height + dy;
                break;
        }

        // 最小尺寸限制
        if (newWidth < Constants.MinWidth) { newWidth = Constants.MinWidth; if (Direction == GripDirection.Left || Direction == GripDirection.TopLeft || Direction == GripDirection.BottomLeft) newLeft = _startLoc.X + _startSize.Width - Constants.MinWidth; }
        if (newHeight < Constants.MinHeight) { newHeight = Constants.MinHeight; if (Direction == GripDirection.Top || Direction == GripDirection.TopLeft || Direction == GripDirection.TopRight) newTop = _startLoc.Y + _startSize.Height - Constants.MinHeight; }

        TargetWindow.Left = newLeft;
        TargetWindow.Top = newTop;
        TargetWindow.Width = newWidth;
        TargetWindow.Height = newHeight;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        MouseMove -= OnMouseMoveDuringDrag;
        MouseLeftButtonUp -= OnMouseUp;
    }
}

internal static class PointExt
{
    public static System.Windows.Point ToWpfPoint(this System.Drawing.Point p)
        => new System.Windows.Point(p.X, p.Y);
}
