using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 「显示外观」子 VM：字体 / 字号 / 行距 / 粗细 / 斜体 / 前景色 / 背景色 / 窗口透明度。
///
/// 从 ReaderViewModel 拆出（体检报告 §1.2 的 God Class 拆分）：
/// 这块只依赖 <see cref="DisplaySettings"/>，与翻页、会话、热键、弹窗完全无关，
/// 是原 ReaderViewModel 里最容易独立、也最该独立的一部分。
/// </summary>
public sealed partial class ReaderDisplayViewModel : ObservableObject
{
    [ObservableProperty] private Brush? _backgroundBrush;
    [ObservableProperty] private Brush? _foregroundBrush;
    [ObservableProperty] private FontFamily? _fontFamily;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineHeightPixels))]
    private double _fontSize = Constants.DefaultFontSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineHeightPixels))]
    private double _lineHeight = Constants.DefaultLineHeight;

    [ObservableProperty] private FontWeight _fontWeight = FontWeights.Normal;
    [ObservableProperty] private FontStyle _fontStyle = FontStyles.Normal;

    /// <summary>窗口透明度（与 WindowBehaviorService 同步）。</summary>
    [ObservableProperty] private double _windowOpacity = 1.0;

    /// <summary>
    /// 渲染侧显式行高（DIP）= 字号 × 行距系数。
    /// 绑定到 TextBlock.LineHeight（BlockLineHeight），与分页引擎的行高完全一致。
    /// </summary>
    public double LineHeightPixels => FontSize * LineHeight;

    /// <summary>
    /// 把 DisplaySettings 映射到各渲染属性。
    /// 映射本身在 <see cref="ReaderDisplayMapper"/>（纯函数，可单测），这里只负责赋值。
    /// </summary>
    public void Apply(DisplaySettings settings)
    {
        var d = ReaderDisplayMapper.Map(settings);
        FontFamily = d.FontFamily;
        FontSize = d.FontSize;
        LineHeight = d.LineHeight;
        FontWeight = d.FontWeight;
        FontStyle = d.FontStyle;
        ForegroundBrush = d.ForegroundBrush;
        BackgroundBrush = d.BackgroundBrush;
        WindowOpacity = d.WindowOpacity;
    }
}
