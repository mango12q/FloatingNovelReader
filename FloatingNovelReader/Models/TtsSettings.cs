namespace FloatingNovelReader.Models;

/// <summary>朗读引擎。Azure 见计划第四刀，本期只实现 EdgePublic。</summary>
public enum TtsEngine
{
    EdgePublic,
    Azure,
}

/// <summary>
/// 音频缓存模式。
/// DiskOptional（默认）：后台合成时自动落盘，断网可秒播；
/// MemoryOnly：纯内存，关闭即丢；Disabled：不预缓存（每段实时合成）。
/// </summary>
public enum TtsCacheMode
{
    MemoryOnly,
    DiskOptional,
    Disabled,
}

/// <summary>
/// 朗读设置。序列化到 settings.json 的 tts 节点。
/// 字段定义见 edge-tts-plan.md §3（已按修正版实现：去掉了本期不用的 DownloadDir，
/// 并统一用 <see cref="TtsCacheMode"/> 表达"关闭缓存"，不再用 PreCacheChapters=0 重复表达）。
/// </summary>
public sealed class TtsSettings
{
    public TtsEngine Engine { get; set; } = TtsEngine.EdgePublic;

    /// <summary>edge 声音名，例如 zh-CN-XiaoxiaoNeural。</summary>
    public string Voice { get; set; } = "zh-CN-XiaoxiaoNeural";

    /// <summary>语速百分比，范围 -50 .. +100。</summary>
    public double RatePercent { get; set; }

    /// <summary>音量百分比，范围 -50 .. +100。</summary>
    public double VolumePercent { get; set; }

    /// <summary>每段合成结束后是否自动翻页（朗读驱动阅读位置）。</summary>
    public bool AutoAdvancePage { get; set; } = true;

    public TtsCacheMode CacheMode { get; set; } = TtsCacheMode.DiskOptional;

    /// <summary>预合成窗口的章节数；0 表示不预合成。</summary>
    public int PreCacheChapters { get; set; } = 2;

    public int CacheMemoryLimitMB { get; set; } = 100;
    public int CacheDiskLimitMB { get; set; } = 500;

    /// <summary>「朗读 N 分钟」的默认分钟数。</summary>
    public int MaxMinutesDefault { get; set; } = 30;

    /// <summary>「朗读 N 章」的默认章节数。</summary>
    public int MaxChaptersDefault { get; set; } = 10;
}
