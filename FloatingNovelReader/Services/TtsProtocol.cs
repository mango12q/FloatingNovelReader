using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FloatingNovelReader.Services;

/// <summary>
/// edge-tts（Microsoft Edge 大声朗读）协议常量与帧构造/解析。
///
/// **纯静态函数，不涉及网络**，因此可以逐条单测 —— 这是把协议从 WebSocket 客户端里
/// 拆出来的目的。所有细节均已按 rany2/edge-tts master 核对，并用真实连接实测过
/// （见 edge-tts-plan.md §4 与 §13）。
/// </summary>
public static class TtsProtocol
{
    /// <summary>公开常量，所有客户端共用。</summary>
    public const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

    /// <summary>Edge 主程序版本；必须与 User-Agent 里的 Edge/Chrome 主版本一致。</summary>
    public const string ChromiumFullVersion = "143.0.3650.75";

    /// <summary>User-Agent 里的主版本号。</summary>
    public const string ChromiumMajor = "143";

    public const string OutputFormat = "audio-24khz-48kbitrate-mono-mp3";

    /// <summary>单次请求的文本上限（**UTF-8 字节**，不是字符数）。</summary>
    public const int MaxRequestBytes = 4096;

    public const string BaseUrl = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";

    public const string Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";

    /// <summary>Sec-MS-GEC-Version = "1-" + ChromiumFullVersion。</summary>
    public static string SecMsGecVersion => "1-" + ChromiumFullVersion;

    public static string UserAgent =>
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        $"(KHTML, like Gecko) Chrome/{ChromiumMajor}.0.0.0 Safari/537.36 " +
        $"Edg/{ChromiumMajor}.0.0.0";

    /// <summary>ConnectionId 必须是**去掉短横线**的 UUID hex。</summary>
    public static string NewConnectionId() => Guid.NewGuid().ToString("N");

    public static string NewRequestId() => Guid.NewGuid().ToString("N");

    /// <summary>Cookie 用的 muid：32 位大写 hex。</summary>
    public static string NewMuid() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Sec-MS-GEC 令牌。
    ///
    /// 三处极易写错的地方（原稿全错）：
    ///   1. 时间戳要**向下取整到 5 分钟**（3_000_000_000 × 100ns = 300s）；
    ///   2. 哈希输入是 **ticks 在前、TOKEN 在后**；
    ///   3. 输出是**大写** hex。
    /// 时间戳是 Windows FILETIME（100ns 为单位、1601 纪元）= DateTime.ToFileTimeUtc()。
    /// </summary>
    public static string BuildSecMsGec(DateTime utcNow)
    {
        var ticks = utcNow.ToFileTimeUtc();
        ticks -= ticks % 3_000_000_000L;

        var toHash = ticks.ToString(CultureInfo.InvariantCulture) + TrustedClientToken;
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(toHash));
        return Convert.ToHexString(hash); // ToHexString 已是大写
    }

    /// <summary>完整的 WebSocket 地址（含全部 query）。</summary>
    public static string BuildWssUrl(DateTime utcNow, string connectionId) =>
        $"wss://{BaseUrl}/edge/v1" +
        $"?TrustedClientToken={TrustedClientToken}" +
        $"&ConnectionId={connectionId}" +
        $"&Sec-MS-GEC={BuildSecMsGec(utcNow)}" +
        $"&Sec-MS-GEC-Version={SecMsGecVersion}";

    /// <summary>JS 风格日期串，服务端要求的格式（末尾的 Z 由调用方按帧决定是否追加）。</summary>
    public static string JsDate(DateTime utcNow) =>
        utcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'",
            CultureInfo.InvariantCulture);

    /// <summary>第 1 帧：Path:speech.config（**不带** X-RequestId）。</summary>
    public static string BuildSpeechConfigFrame(DateTime utcNow)
    {
        // 花括号刻意放在非插值字符串里：在 $"..." 中 "}}" 会被当成转义，只产出 1 个 '}'
        // （原稿的探针第一次跑就是因为少了一个花括号被服务端 InvalidPayloadData 关连接）。
        const string json =
            "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{"
            + "\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"},"
            + "\"outputFormat\":\"" + OutputFormat + "\""
            + "}}}}";

        return "X-Timestamp:" + JsDate(utcNow) + "\r\n" +
               "Content-Type:application/json; charset=utf-8\r\n" +
               "Path:speech.config\r\n\r\n" +
               json + "\r\n";
    }

    /// <summary>第 2 帧：Path:ssml（**必须带 Path:ssml 头**）。</summary>
    /// <param name="escapedText">
    /// 已 XML 转义、已清洗的文本（见 <see cref="TtsText.Prepare"/>）。
    /// 这里**不再**转义，避免二次转义。
    /// </param>
    public static string BuildSsmlFrame(
        string requestId,
        DateTime utcNow,
        string voice,
        string escapedText,
        double ratePercent,
        double volumePercent)
    {
        var ssml =
            "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'>" +
            $"<voice name='{voice}'>" +
            $"<prosody pitch='+0Hz' rate='{SignedPercent(ratePercent)}' volume='{SignedPercent(volumePercent)}'>" +
            escapedText +
            "</prosody></voice></speak>";

        return $"X-RequestId:{requestId}\r\n" +
               "Content-Type:application/ssml+xml\r\n" +
               $"X-Timestamp:{JsDate(utcNow)}Z\r\n" +   // 末尾这个 Z 是微软的 bug，但必须照发
               "Path:ssml\r\n\r\n" +
               ssml;
    }

    /// <summary>把 -50 变成 "-50%"，0 变成 "+0%"，12 变成 "+12%"。</summary>
    public static string SignedPercent(double value)
    {
        var v = Math.Round(value);
        return (v >= 0 ? "+" : "") + v.ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// 解析二进制音频帧。
    ///
    /// 布局（**实测确认**）：
    ///   [2 字节大端 header 长度 L][L 字节 header 文本][音频负载]
    ///   → 负载起点 = L + 2，header 文本 = data[2 .. 2+L]
    /// header 文本**没有空行结尾**，所以不能用 "\r\n\r\n" 定位。
    /// </summary>
    public static bool TryParseBinaryAudioFrame(byte[] data, out string headerBlock, out byte[] payload)
    {
        headerBlock = string.Empty;
        payload = Array.Empty<byte>();

        if (data.Length < 2) return false;

        var length = (data[0] << 8) | data[1];
        if (length <= 0 || 2 + length > data.Length) return false;

        headerBlock = Encoding.UTF8.GetString(data, 2, length);
        var payloadStart = 2 + length;
        var payloadLength = data.Length - payloadStart;
        if (payloadLength <= 0) return false;

        payload = new byte[payloadLength];
        Buffer.BlockCopy(data, payloadStart, payload, 0, payloadLength);
        return true;
    }

    /// <summary>从 header 文本里读某个头的值（大小写敏感，与协议一致）。</summary>
    public static string? HeaderValue(string headerBlock, string key)
    {
        foreach (var line in headerBlock.Split("\r\n"))
        {
            var colon = line.IndexOf(':');
            if (colon > 0 && line.AsSpan(0, colon).Trim().SequenceEqual(key))
                return line[(colon + 1)..].Trim();
        }
        return null;
    }

    /// <summary>负载是否是 MP3 帧同步（0xFF Ex）——用来验证帧偏移是否算对。</summary>
    public static bool LooksLikeMp3(byte[] payload) =>
        payload.Length >= 2 && payload[0] == 0xFF && (payload[1] & 0xE0) == 0xE0;
}
