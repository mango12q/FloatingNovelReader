using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FloatingNovelReader.Core;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>一个可选声音。</summary>
public sealed record TtsVoice(string ShortName, string Locale, string Gender)
{
    public string Display => $"{ShortName}   ({Locale} · {Gender})";
}

/// <summary>
/// 声音目录：优先读本地缓存，其次联网拉一次并缓存；联网失败则用内置兜底列表。
///
/// 列表来自 edge 的声音 REST 接口（公开令牌，与协议同源）。
/// JSON 解析是纯函数 <see cref="ParseVoices"/>，可单测。
/// </summary>
public sealed class TtsVoiceCatalog
{
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(7);

    /// <summary>联网失败时的兜底：常用的中文声音。</summary>
    private static readonly TtsVoice[] FallbackVoices =
    {
        new("zh-CN-XiaoxiaoNeural", "zh-CN", "Female"),
        new("zh-CN-XiaoyiNeural", "zh-CN", "Female"),
        new("zh-CN-YunxiNeural", "zh-CN", "Male"),
        new("zh-CN-YunjianNeural", "zh-CN", "Male"),
        new("zh-CN-YunyangNeural", "zh-CN", "Male"),
        new("zh-CN-YunxiaNeural", "zh-CN", "Male"),
        new("zh-CN-liaoning-XiaobeiNeural", "zh-CN-liaoning", "Female"),
        new("zh-CN-shaanxi-XiaoniNeural", "zh-CN-shaanxi", "Female"),
        new("zh-HK-HiuMaanNeural", "zh-HK", "Female"),
        new("zh-TW-HsiaoChenNeural", "zh-TW", "Female"),
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public IReadOnlyList<TtsVoice> Voices { get; private set; } = FallbackVoices;

    /// <summary>是否用了兜底列表（联网失败时 true，界面可据此提示）。</summary>
    public bool IsFallback { get; private set; } = true;

    private static string CacheFile => Path.Combine(Constants.AppDataDir, "voices.json");

    /// <summary>加载声音列表：缓存 → 联网 → 兜底。</summary>
    public async Task<IReadOnlyList<TtsVoice>> LoadAsync(CancellationToken ct = default)
    {
        // 1. 缓存够新就直接用
        var cached = TryReadCache();
        if (cached.Count > 0)
        {
            Voices = OrderVoices(cached);
            IsFallback = false;
            return Voices;
        }

        // 2. 联网拉一次
        try
        {
            var json = await _http.GetStringAsync(VoicesUrl(), ct).ConfigureAwait(false);
            var parsed = ParseVoices(json);
            if (parsed.Count > 0)
            {
                Voices = OrderVoices(parsed);
                IsFallback = false;
                WriteCache(json);
                Log.Information("声音列表已更新：{Count} 个", Voices.Count);
                return Voices;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "拉取声音列表失败，使用内置兜底列表");
        }

        // 3. 兜底
        Voices = OrderVoices(FallbackVoices);
        IsFallback = true;
        return Voices;
    }

    private static string VoicesUrl() =>
        $"https://{TtsProtocol.BaseUrl}/voices/list?trustedclienttoken={TtsProtocol.TrustedClientToken}";

    /// <summary>
    /// 解析声音接口返回的 JSON 数组。元素里取 ShortName / Locale / Gender；
    /// 缺字段或格式不对的元素直接跳过（不能因为一个坏元素让整个列表失败）。
    /// </summary>
    public static IReadOnlyList<TtsVoice> ParseVoices(string? json)
    {
        var result = new List<TtsVoice>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var shortName = GetString(item, "ShortName");
                if (string.IsNullOrWhiteSpace(shortName)) continue;

                result.Add(new TtsVoice(
                    shortName!,
                    GetString(item, "Locale") ?? "",
                    GetString(item, "Gender") ?? ""));
            }
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "声音列表 JSON 解析失败");
            return new List<TtsVoice>();
        }

        return result;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>中文声音排前面（本项目是中文小说阅读器），其余按名字排；同名去重。</summary>
    public static IReadOnlyList<TtsVoice> OrderVoices(IEnumerable<TtsVoice> voices) =>
        voices
            .GroupBy(v => v.ShortName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(v => v.Locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            .ThenBy(v => v.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<TtsVoice> TryReadCache()
    {
        try
        {
            var file = CacheFile;
            if (!File.Exists(file)) return new List<TtsVoice>();
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > CacheMaxAge) return new List<TtsVoice>();
            return ParseVoices(File.ReadAllText(file)).ToList();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取声音缓存失败（忽略）");
            return new List<TtsVoice>();
        }
    }

    private static void WriteCache(string json)
    {
        try
        {
            Directory.CreateDirectory(Constants.AppDataDir);
            File.WriteAllText(CacheFile, json);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "写入声音缓存失败（忽略）");
        }
    }
}
