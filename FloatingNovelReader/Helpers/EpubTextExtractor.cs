using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Helpers;

/// <summary>
/// EPUB → <see cref="ExtractedContent"/>。
/// 流程：META-INF/container.xml → OPF →（manifest + spine）→ 按 spine 顺序读 XHTML → 转纯文本。
/// 章节标题优先取正文里的 h1~h6，其次取 EPUB 目录（nav.xhtml / toc.ncx）的条目名。
/// 一个 XHTML 里塞了整本书的情况按 h1~h6 再切分（见 AddDocumentSections）。
/// </summary>
public static class EpubTextExtractor
{
    private static readonly Regex ReRootFile = new(
        @"<rootfile\b[^>]*full-path\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReNavLink = new(
        @"<a\b[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReNavBlock = new(
        @"<nav\b[^>]*epub:type\s*=\s*[""']toc[""'][^>]*>(.*?)</nav\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReNcxEntry = new(
        @"<navLabel\b[^>]*>\s*<text\b[^>]*>(.*?)</text\s*>.*?<content\b[^>]*src\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ExtractedContent Extract(string filePath)
    {
        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(filePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("这个文件不是有效的 EPUB（zip 打不开，可能已损坏或被加密）。", ex);
        }

        using (zip)
        {
            var opfPath = FindOpfPath(zip)
                ?? throw new InvalidOperationException("这个 EPUB 缺少 META-INF/container.xml，无法定位正文。");

            var opfEntry = FindEntry(zip, opfPath)
                ?? throw new InvalidOperationException($"这个 EPUB 的包文件缺失（{opfPath}）。");

            var opf = LoadXml(opfEntry)
                ?? throw new InvalidOperationException("这个 EPUB 的包文件（OPF）无法解析，可能已损坏。");

            var content = new ExtractedContent
            {
                Title = FirstNonEmpty(ReadMetadata(opf, "title")) ?? string.Empty,
                Author = FirstNonEmpty(ReadMetadata(opf, "creator")),
            };

            var labels = BuildTocLabels(zip, opf, opfPath);
            var spine = ReadSpine(opf, opfPath);

            if (spine.Count == 0)
            {
                // 极少数 EPUB 没有 spine：退化成按 zip 内的 XHTML 顺序读
                spine = zip.Entries
                    .Where(e => IsHtmlEntry(e.FullName))
                    .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                    .Select(e => e.FullName.Replace('\\', '/'))
                    .ToList();
            }

            int autoIndex = 0;
            foreach (var docPath in spine)
            {
                var entry = FindEntry(zip, docPath);
                if (entry == null)
                {
                    Log.Warning("EPUB 清单里的文件不存在，跳过: {Path}", docPath);
                    continue;
                }

                string html;
                try
                {
                    html = ReadText(entry);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "读取 EPUB 章节失败，跳过: {Path}", docPath);
                    continue;
                }

                var plain = HtmlTextConverter.ToPlainText(html);
                var fallbackTitle = labels.TryGetValue(docPath, out var label) && !string.IsNullOrWhiteSpace(label)
                    ? label.Trim()
                    : HtmlTextConverter.ExtractHeadingTitle(html) ?? $"第 {autoIndex + 1} 章";

                AddDocumentSections(content, plain, fallbackTitle, ref autoIndex);
            }

            // 标题页 / 空章节（只有标题没有正文）对阅读没有意义，去掉
            var meaningful = content.Sections.Where(s => s.Text.Trim().Length > 0).ToList();
            if (meaningful.Count == 0)
                throw new InvalidOperationException("这个 EPUB 里没有解析出正文（可能是纯图片漫画，或受 DRM 保护）。");

            if (meaningful.Count != content.Sections.Count)
            {
                content.Sections.Clear();
                content.Sections.AddRange(meaningful);
            }

            Log.Information("EPUB 解析完成 {File}: 章节={Count} 字符={Chars}",
                Path.GetFileName(filePath), content.Sections.Count, content.TotalChars);
            return content;
        }
    }

    /// <summary>把一个 XHTML 的纯文本按 h1~h6 切成若干章节段；没有标题就当一整段。</summary>
    private static void AddDocumentSections(
        ExtractedContent content, HtmlTextResult plain, string fallbackTitle, ref int autoIndex)
    {
        var text = plain.Text;
        if (text.Length == 0) return;

        if (plain.Headings.Count == 0)
        {
            var body = text.Trim();
            if (body.Length == 0) return;
            content.Sections.Add(new ExtractedSection(fallbackTitle, body));
            autoIndex++;
            return;
        }

        // 第一个标题之前的内容（封面文案、前言…）
        var first = plain.Headings[0];
        if (first.Index > 0)
        {
            var pre = text[..first.Index].Trim();
            if (pre.Length > 0)
            {
                content.Sections.Add(new ExtractedSection(fallbackTitle, pre));
                autoIndex++;
            }
        }

        for (int i = 0; i < plain.Headings.Count; i++)
        {
            var h = plain.Headings[i];
            int bodyStart = h.Index + h.Title.Length;
            if (bodyStart < text.Length && text[bodyStart] == '\n') bodyStart++;

            int bodyEnd = i + 1 < plain.Headings.Count ? plain.Headings[i + 1].Index : text.Length;
            if (bodyEnd < bodyStart) bodyEnd = bodyStart;

            var body = text[bodyStart..bodyEnd].Trim();
            content.Sections.Add(new ExtractedSection(h.Title, body));
            autoIndex++;
        }
    }

    // ── OPF ─────────────────────────────────────────────

    private static string? FindOpfPath(ZipArchive zip)
    {
        var container = FindEntry(zip, "META-INF/container.xml");
        if (container == null) return null;

        var xml = ReadText(container);
        var doc = TryParseXml(xml);
        if (doc != null)
        {
            var rootFile = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "rootfile");
            var path = rootFile?.Attribute("full-path")?.Value;
            if (!string.IsNullOrWhiteSpace(path)) return NormalizePath(path);
        }

        // XML 残缺时退回正则
        var m = ReRootFile.Match(xml);
        return m.Success ? NormalizePath(m.Groups[1].Value) : null;
    }

    private static XDocument? LoadXml(ZipArchiveEntry entry)
    {
        var text = ReadText(entry);
        return TryParseXml(text);
    }

    private static XDocument? TryParseXml(string xml)
    {
        try
        {
            return XDocument.Parse(xml, LoadOptions.None);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> ReadMetadata(XDocument opf, string localName)
    {
        foreach (var e in opf.Descendants().Where(e => e.Name.LocalName == localName))
        {
            var v = e.Value?.Trim();
            if (!string.IsNullOrEmpty(v)) yield return v;
        }
    }

    private static string? FirstNonEmpty(IEnumerable<string> values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>按 spine 顺序取出正文文档路径（linear="no" 的跳过）。</summary>
    private static List<string> ReadSpine(XDocument opf, string opfPath)
    {
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in opf.Descendants().Where(e => e.Name.LocalName == "item"))
        {
            var id = item.Attribute("id")?.Value;
            var href = item.Attribute("href")?.Value;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(href)) continue;
            manifest[id] = ResolveHref(opfPath, href);
        }

        var result = new List<string>();
        foreach (var itemref in opf.Descendants().Where(e => e.Name.LocalName == "itemref"))
        {
            var linear = itemref.Attribute("linear")?.Value;
            if (string.Equals(linear, "no", StringComparison.OrdinalIgnoreCase)) continue;

            var idref = itemref.Attribute("idref")?.Value;
            if (string.IsNullOrWhiteSpace(idref)) continue;
            if (manifest.TryGetValue(idref, out var path)) result.Add(path);
        }
        return result;
    }

    /// <summary>EPUB 目录（nav.xhtml 优先，其次 toc.ncx）→ 「文档路径 → 标题」。</summary>
    private static Dictionary<string, string> BuildTocLabels(ZipArchive zip, XDocument opf, string opfPath)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. EPUB3 nav
        var navItem = opf.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "item" &&
            (e.Attribute("properties")?.Value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(p => string.Equals(p, "nav", StringComparison.OrdinalIgnoreCase)));

        var navHref = navItem?.Attribute("href")?.Value;
        if (!string.IsNullOrWhiteSpace(navHref))
            AddNavLabels(zip, labels, ResolveHref(opfPath, navHref));

        // 2. EPUB2 NCX
        var ncxItem = opf.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "item" &&
            string.Equals(e.Attribute("media-type")?.Value, "application/x-dtbncx+xml", StringComparison.OrdinalIgnoreCase));
        var ncxHref = ncxItem?.Attribute("href")?.Value;
        if (!string.IsNullOrWhiteSpace(ncxHref))
            AddNcxLabels(zip, labels, ResolveHref(opfPath, ncxHref));

        return labels;
    }

    private static void AddNavLabels(ZipArchive zip, Dictionary<string, string> labels, string navPath)
    {
        var entry = FindEntry(zip, navPath);
        if (entry == null) return;

        var xml = ReadText(entry);
        var block = ReNavBlock.Match(xml);
        var scope = block.Success ? block.Groups[1].Value : xml;

        foreach (Match m in ReNavLink.Matches(scope))
        {
            var href = ResolveHref(navPath, m.Groups[1].Value);
            var title = HtmlTextConverter.CleanInline(m.Groups[2].Value);
            if (href.Length == 0 || title.Length == 0) continue;
            labels.TryAdd(href, title);
        }
    }

    private static void AddNcxLabels(ZipArchive zip, Dictionary<string, string> labels, string ncxPath)
    {
        var entry = FindEntry(zip, ncxPath);
        if (entry == null) return;

        var xml = ReadText(entry);
        foreach (Match m in ReNcxEntry.Matches(xml))
        {
            var href = ResolveHref(ncxPath, m.Groups[2].Value);
            var title = HtmlTextConverter.CleanInline(m.Groups[1].Value);
            if (href.Length == 0 || title.Length == 0) continue;
            labels.TryAdd(href, title);
        }
    }

    // ── zip 工具 ────────────────────────────────────────

    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string path)
    {
        var normalized = NormalizePath(path);
        var entry = zip.GetEntry(normalized);
        if (entry != null) return entry;

        // zip 条目名大小写敏感的坑：EPUB 里 OPF 与实际文件名大小写不一致很常见
        foreach (var e in zip.Entries)
        {
            if (string.Equals(NormalizePath(e.FullName), normalized, StringComparison.OrdinalIgnoreCase))
                return e;
        }
        return null;
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static bool IsHtmlEntry(string name)
    {
        var n = name.ToLowerInvariant();
        return n.EndsWith(".xhtml") || n.EndsWith(".html") || n.EndsWith(".htm");
    }

    /// <summary>把 href 解析成 zip 内的绝对路径（去锚点/查询串、解百分号转义、折叠 ../）。</summary>
    internal static string ResolveHref(string basePath, string href)
    {
        var value = href ?? string.Empty;

        int hash = value.IndexOf('#');
        if (hash >= 0) value = value[..hash];
        int query = value.IndexOf('?');
        if (query >= 0) value = value[..query];

        try
        {
            value = Uri.UnescapeDataString(value);
        }
        catch
        {
            // 非法转义序列：按原样使用
        }

        if (value.Length == 0) return NormalizePath(basePath);

        var baseDir = string.Empty;
        var normalizedBase = NormalizePath(basePath);
        int slash = normalizedBase.LastIndexOf('/');
        if (slash >= 0) baseDir = normalizedBase[..slash];

        var combined = value.StartsWith('/')
            ? value.TrimStart('/')
            : (baseDir.Length == 0 ? value : baseDir + "/" + value);

        return NormalizePath(combined);
    }

    /// <summary>折叠 ./ 与 ../，统一分隔符。</summary>
    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var parts = new List<string>();
        foreach (var seg in path.Replace('\\', '/').Split('/'))
        {
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(seg);
        }
        return string.Join('/', parts);
    }
}
