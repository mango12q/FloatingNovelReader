namespace FloatingNovelReader.Core;

/// <summary>
/// 全局常量集中定义。
/// </summary>
public static class Constants
{
    // 应用元数据
    public const string AppName = "浮窗小说阅读器";
    public const string AppVersion = "1.0.0";
    public const string MutexName = "FloatingNovelReader.SingleInstance";

    // 路径
    public static string AppDataDir => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloatingNovelReader");

    public static string LogDir => System.IO.Path.Combine(AppDataDir, "Logs");
    public static string DbFile => System.IO.Path.Combine(AppDataDir, "library.db");
    public static string SettingsFile => System.IO.Path.Combine(AppDataDir, "settings.json");

    /// <summary>朗读音频缓存目录（DiskOptional 模式下后台合成的 mp3 落在这里）。</summary>
    public static string TtsCacheDir => System.IO.Path.Combine(AppDataDir, "TtsCache");

    // 窗口尺寸默认值
    public const double DefaultWidth = 500;
    public const double DefaultHeight = 700;
    public const double MinWidth = 300;
    public const double MinHeight = 200;
    public const int EdgeSnapThreshold = 15;

    // 透明度
    public const double MinOpacity = 0.20;
    public const double MaxOpacity = 1.00;
    public const double OpacityStep = 0.05;

    // 控制栏
    public const int ControlBarShowDelayMs = 300;
    public const int ControlBarHideDelayMs = 1500;
    public const int ControlBarHeight = 40;
    public const int ResizeBorderWidth = 5;

    // 字体
    public const int MinFontSize = 12;
    public const int MaxFontSize = 48;
    public const int DefaultFontSize = 18;
    public const double MinLineHeight = 1.0;
    public const double MaxLineHeight = 2.5;
    public const double DefaultLineHeight = 1.5;

    // 自动阅读
    public const int DefaultAutoReadIntervalSec = 10;
    public const int MinAutoReadIntervalSec = 1;
    public const int MaxAutoReadIntervalSec = 60;

    // 防抖
    public const int HotkeyDebounceMs = 150;
    public const int ProgressSaveDebounceMs = 500;

    // 导入上限。Import() 会把整份文件读进内存、解析器再复制一份，
    // 不设上限时一个超大 TXT 就能把进程撑爆（而且报错完全没线索）。
    public const long MaxImportFileBytes = 200L * 1024 * 1024; // 200 MB
}
