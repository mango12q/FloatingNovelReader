using System.Collections.Generic;
using System.ComponentModel;
using FloatingNovelReader.Models;
using FloatingNovelReader.ViewModels;
using Xunit;

namespace FloatingNovelReader.Tests.ViewModels;

/// <summary>
/// 外观子 VM：DisplaySettings → 渲染属性。
/// 拆分前这段逻辑和翻页、会话、热键混在 ReaderViewModel 里，无法独立测试。
/// </summary>
public class ReaderDisplayViewModelTests
{
    [Fact]
    public void Apply_MapsEveryDisplayProperty()
    {
        var vm = new ReaderDisplayViewModel();

        vm.Apply(new DisplaySettings
        {
            FontFamily = "SimSun",
            FontSize = 20,
            LineHeight = 1.6,
            FontBold = true,
            FontItalic = true,
            FontColorPreset = FontColorPreset.White,
            BackgroundPreset = BackgroundPreset.Gray,
            Opacity = 0.75,
        });

        Assert.Equal("SimSun", vm.FontFamily!.Source);
        Assert.Equal(20, vm.FontSize);
        Assert.Equal(1.6, vm.LineHeight);
        Assert.Equal(System.Windows.FontWeights.Bold, vm.FontWeight);
        Assert.Equal(System.Windows.FontStyles.Italic, vm.FontStyle);
        Assert.Equal(0.75, vm.WindowOpacity);
        Assert.NotNull(vm.BackgroundBrush);
        Assert.NotNull(vm.ForegroundBrush);
    }

    [Fact]
    public void Apply_UpdatesLineHeightPixels()
    {
        var vm = new ReaderDisplayViewModel();
        vm.Apply(new DisplaySettings { FontSize = 20, LineHeight = 1.5 });

        Assert.Equal(30, vm.LineHeightPixels, 3);
    }

    [Fact]
    public void LineHeightPixels_IsRaisedWhenFontSizeChanges()
    {
        var vm = new ReaderDisplayViewModel();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

        vm.FontSize = 30;

        Assert.Contains(nameof(ReaderDisplayViewModel.LineHeightPixels), raised);
    }

    [Fact]
    public void LineHeightPixels_IsRaisedWhenLineHeightChanges()
    {
        var vm = new ReaderDisplayViewModel();
        var raised = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

        vm.LineHeight = 2.0;

        Assert.Contains(nameof(ReaderDisplayViewModel.LineHeightPixels), raised);
    }

    [Fact]
    public void Apply_TransparentPreset_YieldsTransparentBrush()
    {
        var vm = new ReaderDisplayViewModel();
        vm.Apply(new DisplaySettings { BackgroundPreset = BackgroundPreset.Transparent });

        Assert.Same(System.Windows.Media.Brushes.Transparent, vm.BackgroundBrush);
    }
}
