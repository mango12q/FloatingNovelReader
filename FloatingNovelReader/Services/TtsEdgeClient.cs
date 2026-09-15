using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// edge-tts WebSocket 客户端：把一个文本片段合成成 mp3 字节。
///
/// 协议细节全部在 <see cref="TtsProtocol"/>（纯函数、已单测）；这里只负责连接与收发。
/// 帧布局（实测）：[2 字节大端 header 长度][header 文本][音频负载]。
///
/// 已知限制：参考实现在握手 403 时会用响应的 Date 头做时钟倾斜校正再重试，
/// 而 <see cref="ClientWebSocket"/> 不暴露握手响应的响应头，所以这里退化为
/// "用新时间戳/新令牌重试一次"。绝大多数 403 是令牌过期或时段取整造成的，重试可覆盖。
/// </summary>
public sealed class TtsEdgeClient
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(60);

    /// <summary>合成一段文本；失败会重试一次，最终仍失败则抛出。</summary>
    public async Task<byte[]> SynthesizeAsync(TtsSettings settings, string escapedText, CancellationToken ct)
    {
        try
        {
            return await SynthesizeOnceAsync(settings, escapedText, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "edge-tts 合成失败，用新的时间戳重试一次");
            await Task.Delay(600, ct);
            return await SynthesizeOnceAsync(settings, escapedText, ct);
        }
    }

    private static async Task<byte[]> SynthesizeOnceAsync(
        TtsSettings settings, string escapedText, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        // 全部在探针里实测可用；SetRequestHeader 对个别头可能抛，逐个兜住
        TryHeader(ws, "Pragma", "no-cache");
        TryHeader(ws, "Cache-Control", "no-cache");
        TryHeader(ws, "Origin", TtsProtocol.Origin);
        TryHeader(ws, "User-Agent", TtsProtocol.UserAgent);
        TryHeader(ws, "Cookie", $"muid={TtsProtocol.NewMuid()};");
        TryHeader(ws, "Accept-Encoding", "gzip, deflate, br, zstd");
        TryHeader(ws, "Accept-Language", "en-US,en;q=0.9");

        var url = TtsProtocol.BuildWssUrl(now, TtsProtocol.NewConnectionId());
        await ws.ConnectAsync(new Uri(url), ct);

        await SendTextAsync(ws, TtsProtocol.BuildSpeechConfigFrame(now), ct);
        await SendTextAsync(ws, TtsProtocol.BuildSsmlFrame(
            TtsProtocol.NewRequestId(), now, settings.Voice, escapedText,
            settings.RatePercent, settings.VolumePercent), ct);

        var audio = new MemoryStream();
        var buffer = new byte[16 * 1024];
        var message = new MemoryStream();

        while (true)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ReceiveTimeout);

            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("等待 edge-tts 响应超时");
            }

            if (result.MessageType == WebSocketMessageType.Close)
                throw new IOException($"服务端提前关闭连接: {ws.CloseStatus} {ws.CloseStatusDescription}");

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var data = message.ToArray();
            message.SetLength(0);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var text = System.Text.Encoding.UTF8.GetString(data);
                var separator = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                var header = separator >= 0 ? text[..separator] : text;
                var path = TtsProtocol.HeaderValue(header, "Path");

                if (path == "turn.end") break;
                // turn.start / response / audio.metadata 都无需处理
                continue;
            }

            // 二进制帧：**无条件拼接**（不按 MP3 同步字过滤 —— 后续帧可能从帧中间开始）
            if (TtsProtocol.TryParseBinaryAudioFrame(data, out _, out var payload) && payload.Length > 0)
                audio.Write(payload, 0, payload.Length);
        }

        if (audio.Length == 0)
            throw new IOException("合成没有返回音频（服务端静默截断）");

        Log.Debug("edge-tts 合成完成 {Bytes} 字节", audio.Length);
        return audio.ToArray();
    }

    private static void TryHeader(ClientWebSocket ws, string name, string value)
    {
        try { ws.Options.SetRequestHeader(name, value); }
        catch (Exception ex) { Log.Debug(ex, "设置请求头被拒绝: {Name}", name); }
    }

    private static Task SendTextAsync(ClientWebSocket ws, string text, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}
