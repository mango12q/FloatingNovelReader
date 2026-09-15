using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;

namespace FloatingNovelReader.ViewModels;

/// <summary>
/// 设置页「朗读」Tab 的 VM。
///
/// 直接读写 <c>SettingsService.Current.Tts</c>，所以窗口的"保存 / 取消"天然生效：
/// 保存 = 序列化 Current；取消 = Reload 换掉 Current，随后调用 <see cref="Refresh"/> 把界面拉回。
/// 只暴露**当前真的生效**的设置项（引擎/Azure、缓存、N 分钟等留给后续刀，避免出现"改了没用"的开关）。
/// </summary>
public sealed partial class TtsPanelViewModel : ObservableObject
{
    private const string PreviewText = "这是一段试听文本，用来确认音色和语速是否合适。";

    private readonly SettingsService _settings;
    private readonly TtsVoiceCatalog _catalog;
    private readonly TtsService _tts;

    public ObservableCollection<TtsVoice> Voices { get; } = new();

    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isLoadingVoices;

    public TtsPanelViewModel(SettingsService settings, TtsVoiceCatalog catalog, TtsService tts)
    {
        _settings = settings;
        _catalog = catalog;
        _tts = tts;
    }

    private TtsSettings Tts => _settings.Current.Tts;

    public string Voice
    {
        get => Tts.Voice;
        set
        {
            var v = value ?? string.Empty;
            if (string.Equals(Tts.Voice, v, StringComparison.Ordinal)) return;
            Tts.Voice = v;
            OnPropertyChanged();
        }
    }

    public double RatePercent
    {
        get => Tts.RatePercent;
        set
        {
            var v = Math.Clamp(value, -50, 100);
            if (Math.Abs(Tts.RatePercent - v) < 0.01) return;
            Tts.RatePercent = v;
            OnPropertyChanged();
        }
    }

    public double VolumePercent
    {
        get => Tts.VolumePercent;
        set
        {
            var v = Math.Clamp(value, -50, 100);
            if (Math.Abs(Tts.VolumePercent - v) < 0.01) return;
            Tts.VolumePercent = v;
            OnPropertyChanged();
        }
    }

    public bool AutoAdvancePage
    {
        get => Tts.AutoAdvancePage;
        set
        {
            if (Tts.AutoAdvancePage == value) return;
            Tts.AutoAdvancePage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>加载声音列表：缓存 → 联网 → 内置兜底。</summary>
    [RelayCommand]
    private async Task LoadVoicesAsync()
    {
        if (IsLoadingVoices) return;
        IsLoadingVoices = true;
        Status = "正在获取声音列表…";
        try
        {
            var voices = await _catalog.LoadAsync().ConfigureAwait(true);
            Voices.Clear();
            foreach (var v in voices) Voices.Add(v);

            // 配置里的声音若不在列表里，仍要能显示出来（否则 ComboBox 会显示空白）
            if (!string.IsNullOrWhiteSpace(Voice) && Voices.All(v => v.ShortName != Voice))
                Voices.Insert(0, new TtsVoice(Voice, "?", "?"));

            Status = _catalog.IsFallback
                ? $"联网获取失败，已用内置列表（{Voices.Count} 个）"
                : $"声音列表 {Voices.Count} 个";
        }
        finally
        {
            IsLoadingVoices = false;
        }
    }

    [RelayCommand]
    public void Preview()
    {
        _tts.Speak(PreviewText, drivesReader: false);
        Status = "试听中…（可点『停止试听』）";
    }

    [RelayCommand]
    public void StopPreview()
    {
        _tts.Stop();
        Status = "已停止试听";
    }

    /// <summary>取消 / 恢复默认之后调用：让界面重新读当前设置。</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Voice));
        OnPropertyChanged(nameof(RatePercent));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(AutoAdvancePage));
    }
}
