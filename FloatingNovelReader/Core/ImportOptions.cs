namespace FloatingNovelReader.Core;

/// <summary>
/// 导入相关的可配置项。目前只有一项：EPUB / PDF 解析出的正文缓存目录。
/// 之所以做成对象而不是直接读 <see cref="Constants.ImportCacheDir"/>：
/// 单测需要一个临时目录，否则测试会把缓存写进用户的 %LocalAppData%。
/// </summary>
public sealed class ImportOptions
{
    /// <summary>正文缓存目录（默认 <see cref="Constants.ImportCacheDir"/>）。</summary>
    public string CacheDirectory { get; set; } = Constants.ImportCacheDir;
}
