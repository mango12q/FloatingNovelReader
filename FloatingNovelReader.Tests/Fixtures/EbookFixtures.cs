using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FloatingNovelReader.Tests.Fixtures;

/// <summary>
/// 测试用的电子书构造器。
/// EPUB 是 zip + XHTML，PDF 是带 xref 的二进制，都在这里现场生成，
/// 避免往仓库里塞二进制样本（也无法保证样本的授权）。
/// </summary>
internal static class EbookFixtures
{
    public sealed record EpubDocument(string Href, string Xhtml, string? NavTitle = null);

    /// <summary>
    /// 生成一个结构完整的 EPUB（mimetype + container.xml + OPF + 目录 + 正文）。
    /// </summary>
    /// <param name="useNcx">true 生成 EPUB2 的 toc.ncx，false 生成 EPUB3 的 nav.xhtml</param>
    public static byte[] BuildEpub(
        string title,
        string author,
        IReadOnlyList<EpubDocument> documents,
        bool useNcx = false,
        string? customTitleTag = null)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // mimetype 必须是第一个条目且不压缩（EPUB 规范）
            var mime = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var w = new StreamWriter(mime.Open(), new UTF8Encoding(false)))
                w.Write("application/epub+zip");

            WriteEntry(zip, "META-INF/container.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);

            var opf = new StringBuilder();
            opf.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
            opf.Append("""<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid">""").Append('\n');
            opf.Append("  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
            opf.Append("    <dc:title>").Append(Escape(title)).Append("</dc:title>\n");
            opf.Append("    <dc:creator>").Append(Escape(author)).Append("</dc:creator>\n");
            opf.Append("    <dc:language>zh-CN</dc:language>\n");
            opf.Append("  </metadata>\n");
            opf.Append("  <manifest>\n");

            int index = 0;
            foreach (var doc in documents)
            {
                opf.Append($"""    <item id="doc{index}" href="{Escape(doc.Href)}" media-type="application/xhtml+xml"/>""").Append('\n');
                index++;
            }

            if (useNcx)
                opf.Append("""    <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>""").Append('\n');
            else
                opf.Append("""    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>""").Append('\n');

            opf.Append("  </manifest>\n");
            opf.Append(useNcx ? "  <spine toc=\"ncx\">\n" : "  <spine>\n");
            for (int i = 0; i < documents.Count; i++)
                opf.Append($"""    <itemref idref="doc{i}"/>""").Append('\n');
            opf.Append("  </spine>\n</package>\n");

            WriteEntry(zip, "OEBPS/content.opf", opf.ToString());

            index = 0;
            foreach (var doc in documents)
            {
                WriteEntry(zip, "OEBPS/" + doc.Href, doc.Xhtml);
                index++;
            }

            var titled = documents.Where(d => !string.IsNullOrEmpty(d.NavTitle)).ToList();
            if (useNcx)
            {
                var ncx = new StringBuilder();
                ncx.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
                ncx.Append("""<ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">""").Append('\n');
                ncx.Append("  <navMap>\n");
                int playOrder = 1;
                foreach (var doc in titled)
                {
                    ncx.Append($"    <navPoint id=\"np{playOrder}\" playOrder=\"{playOrder}\">\n");
                    ncx.Append($"      <navLabel><text>{Escape(doc.NavTitle!)}</text></navLabel>\n");
                    ncx.Append($"      <content src=\"{Escape(doc.Href)}\"/>\n");
                    ncx.Append("    </navPoint>\n");
                    playOrder++;
                }
                ncx.Append("  </navMap>\n</ncx>\n");
                WriteEntry(zip, "OEBPS/toc.ncx", ncx.ToString());
            }
            else
            {
                var nav = new StringBuilder();
                nav.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
                nav.Append("""<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">""").Append('\n');
                nav.Append("<head><title>目录</title></head>\n<body>\n");
                nav.Append("""<nav epub:type="toc"><ol>""").Append('\n');
                foreach (var doc in titled)
                    nav.Append($"""<li><a href="{Escape(doc.Href)}">{Escape(doc.NavTitle!)}</a></li>""").Append('\n');
                nav.Append("</ol></nav>\n</body>\n</html>\n");
                WriteEntry(zip, "OEBPS/nav.xhtml", nav.ToString());
            }

            if (customTitleTag != null)
                WriteEntry(zip, "OEBPS/extra.xhtml", customTitleTag);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 生成一个最小但结构合法的 PDF（Helvetica 标准字体 + 每页若干行 ASCII 文本）。
    /// 只用于验证「文字层抽取 + 章节切分」，因此不涉及中文字体嵌入。
    /// </summary>
    public static byte[] BuildPdf(IReadOnlyList<IReadOnlyList<string>> pages)
    {
        int pageCount = pages.Count;
        int firstPageObj = 4;                        // 1 catalog / 2 pages / 3 font
        var objects = new List<string>();

        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{firstPageObj + i * 2} 0 R"));
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");                              // 1
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");           // 2
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"); // 3

        foreach (var lines in pages)
        {
            var content = new StringBuilder();
            content.Append("BT\n/F1 14 Tf\n72 720 Td\n");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) content.Append("0 -22 Td\n");
                content.Append('(').Append(EscapePdfText(lines[i])).Append(") Tj\n");
            }
            content.Append("ET\n");

            var body = content.ToString();
            int pageObjNumber = objects.Count + 1;      // 即将加入的页对象
            int contentObjNumber = pageObjNumber + 1;   // 紧随其后的内容流对象
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjNumber} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(body)} >>\nstream\n{body}endstream");
        }

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        long xrefOffset = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        foreach (var off in offsets)
            Write($"{off:D10} 00000 n \n");

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return ms.ToArray();
    }

    /// <summary>生成一个没有任何文字层的 PDF（模拟扫描版：页面上只有一条空白内容流）。</summary>
    public static byte[] BuildEmptyTextPdf(int pageCount = 2)
    {
        var pages = new List<IReadOnlyList<string>>();
        for (int i = 0; i < pageCount; i++) pages.Add(Array.Empty<string>());
        return BuildPdf(pages);
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string EscapePdfText(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
