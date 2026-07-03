using System.Text;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// CodePages 编码注册。
/// .NET（Core）默认只带 Unicode 系编码，GBK / GB18030 / Big5 等
/// 必须注册 <see cref="CodePagesEncodingProvider"/> 后才能通过
/// <c>Encoding.GetEncoding</c> 获取，否则中文本地编码的 TXT 无法导入和阅读。
/// </summary>
public static class EncodingSupport
{
    private static bool _registered;
    private static readonly object Gate = new();

    /// <summary>确保 CodePages 编码已注册（幂等，可重复调用）。</summary>
    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (Gate)
        {
            if (_registered) return;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _registered = true;
        }
    }
}
