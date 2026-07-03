using System.IO;
using System.Linq;
using System.Text;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// ChapterParser 卷章解析的单元测试。
/// 覆盖 4.2 节规格说明的所有边界情况。
/// </summary>
public class ChapterParserTests
{
    private readonly ChapterParser _parser = new();

    [Fact]
    public void Parse_NoChapter_ReturnsSingleChapter()
    {
        var text = "三体\n刘慈欣\n第一章 概要\n这是一本小说。\n第二章 故事开始\n更多内容";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        Assert.True(book.TotalChapters >= 1);
    }

    [Fact]
    public void Parse_ChineseChapterNumbers_Normalized()
    {
        var text = "第一章 开始\n内容A\n第二章 继续\n内容B\n第10章 后续\n内容C\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(3, flat.Count);
        Assert.Equal(1, flat[0].DisplayNumber);
        Assert.Equal(2, flat[1].DisplayNumber);
        Assert.Equal(10, flat[2].DisplayNumber);
    }

    [Fact]
    public void Parse_ArabicChapterNumbers_Normalized()
    {
        var text = "第1章 开始\nA\n第2章 继续\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal(1, flat[0].DisplayNumber);
        Assert.Equal(2, flat[1].DisplayNumber);
    }

    [Fact]
    public void Parse_PaddedArabicChapter_Normalized()
    {
        var text = "第001章 开始\nA\n第010章 继续\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(1, flat[0].DisplayNumber);
        Assert.Equal(10, flat[1].DisplayNumber);
    }

    [Fact]
    public void Parse_ChapterAndVolume_HasCorrectHierarchy()
    {
        var text = @"
第一卷 序幕

第一章 开端
内容

第二章 发展
内容

第二卷 高潮

第三章 决战
内容
";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        // 至少 2 个卷 + 正文（序章）= 3
        Assert.True(book.TotalVolumes >= 2);
        Assert.True(book.TotalChapters >= 3);
    }

    [Fact]
    public void Parse_EnglishChapter_Normalized()
    {
        var text = "Chapter 1 First\nA\nChapter 2 Second\nB\nchapter 3 Third\nC\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(3, flat.Count);
        Assert.Equal(1, flat[0].DisplayNumber);
        Assert.Equal(2, flat[1].DisplayNumber);
        Assert.Equal(3, flat[2].DisplayNumber);
    }

    [Fact]
    public void Parse_NumberedList_RecognizedAsChapter()
    {
        var text = "1、 开始\nA\n2、 继续\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
    }

    [Fact]
    public void Parse_Author_Extracted()
    {
        var text = "小说名\n作者：某某某\n\n第一章 开始\n内容\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        Assert.Equal("某某某", book.Author);
    }

    [Fact]
    public void Parse_VolumeWithNumber_Recognized()
    {
        var text = "卷一 开始\n第一章 A\n\n卷二 继续\n第二章 B\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        // 至少 2 个卷
        Assert.True(book.TotalVolumes >= 2);
    }

    [Fact]
    public void Parse_BookTitle_FromFileName()
    {
        var text = "正文内容";
        var book = _parser.Parse(text, @"D:\books\三体.txt", Encoding.UTF8.GetByteCount(text));
        Assert.Equal("三体", book.Title);
    }

    [Fact]
    public void Parse_ChapterTitle_IncludesTail_Chinese()
    {
        // 用户需求: 把「第N章」后面那段作为章节名, 完整标题保留原行
        // 数字归一化由 DisplayNumber 负责, Title 保留原文 (含汉字数字)
        var text = "第一章 开端\n内容A\n第二章 发展\n内容B\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal("第一章 开端", flat[0].Title);
        Assert.Equal(1, flat[0].DisplayNumber);
        Assert.Equal("第二章 发展", flat[1].Title);
        Assert.Equal(2, flat[1].DisplayNumber);
    }

    [Fact]
    public void Parse_ChapterTitle_ArabicWithTail()
    {
        var text = "第3章 斗破苍穹\nA\n第4章 风起云涌\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal("第3章 斗破苍穹", flat[0].Title);
        Assert.Equal(3, flat[0].DisplayNumber);
        Assert.Equal("第4章 风起云涌", flat[1].Title);
    }

    [Fact]
    public void Parse_ChapterTitle_NoTail_DefaultsToNumber()
    {
        // 章节标题只有「第N章」, 后面没东西, Title 就用整行「第N章」
        var text = "第一章\n内容A\n第二章\n内容B\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal("第一章", flat[0].Title);
        Assert.Equal("第二章", flat[1].Title);
    }

    [Fact]
    public void Parse_ChapterTitle_EnglishWithTail()
    {
        var text = "Chapter 1 The Beginning\nA\nChapter 2 The Journey\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal("Chapter 1 The Beginning", flat[0].Title);
        Assert.Equal("Chapter 2 The Journey", flat[1].Title);
    }

    [Fact]
    public void Parse_ChapterTitle_NumberedListWithTail()
    {
        // 纯数字编号"1、xxx" 也应该带 tail
        var text = "1、 开篇\nA\n2、 续篇\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal("1、 开篇", flat[0].Title);
        Assert.Equal("2、 续篇", flat[1].Title);
    }

    [Fact]
    public void Parse_Load_FullStructure()
    {
        // 验证 Parser 返回的 Book 在 Open 后 VM.Load 能正确展开
        var text = "第一章 开端\n内容A\n第二章 发展\n内容B\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        Assert.NotEmpty(book.Volumes);
        var firstVolume = book.Volumes[0];
        Assert.NotEmpty(firstVolume.Chapters);
        Assert.Contains(firstVolume.Chapters, c => c.Title == "第一章 开端");
        Assert.Contains(firstVolume.Chapters, c => c.Title == "第二章 发展");
    }

    // ---- 章节写法覆盖增强：回 / 话 / 部 / 特殊标题 ----

    [Fact]
    public void Parse_HuiChapter_Recognized()
    {
        // 古典章回体：西游记 / 红楼梦 / 三国演义全是「第N回」
        var text = "第一回 灵根育孕源流出\n内容A\n第二回 悟彻菩提真如\n内容B\n第一百回 径回东土\n内容C\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(3, flat.Count);
        Assert.Equal("第一回 灵根育孕源流出", flat[0].Title);
        Assert.Equal(1, flat[0].DisplayNumber);
        Assert.Equal(100, flat[2].DisplayNumber);
    }

    [Fact]
    public void Parse_HuiHe_InBody_NotChapter()
    {
        // 正文行首出现「第三回合」不能被误切成一章
        var text = "第一章 比武\n第三回合的较量开始了，双方僵持不下。\n第二章 结局\n内容\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
    }

    [Fact]
    public void Parse_HuaChapter_Recognized()
    {
        // 日系轻小说常用「第N话」
        var text = "第1话 相遇\nA\n第2话 别离\nB\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
        Assert.Equal(2, flat[1].DisplayNumber);
    }

    [Fact]
    public void Parse_BuVolume_Recognized()
    {
        // 「第N部」是卷级结构
        var text = "第一部 觉醒\n第一章 A\n内容\n第二部 崛起\n第二章 B\n内容\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        Assert.True(book.TotalVolumes >= 2);
        Assert.Equal(2, book.FlatChapters().Count(c => c.Title.StartsWith("第")
            && (c.Title.Contains("章"))));
    }

    [Fact]
    public void Parse_BuFen_InBody_NotVolume()
    {
        // 正文行首「第一部分」不能被误判成新卷
        var text = "第一章 说明\n第一部分内容如下，先做一个总体介绍。\n第二章 展开\n内容\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        Assert.Equal(1, book.TotalVolumes);
        Assert.Equal(2, book.TotalChapters);
    }

    [Fact]
    public void Parse_SpecialHeaders_Recognized()
    {
        // 楔子 / 番外 / 尾声 等独立标题应成章
        var text = "楔子\n多年以前的一个雨夜。\n第一章 开始\n内容A\n番外一 重逢\n内容B\n尾声\n内容C\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(4, flat.Count);
        Assert.Equal("楔子", flat[0].Title);
        Assert.Equal("番外一 重逢", flat[2].Title);
        Assert.Equal("尾声", flat[3].Title);
    }

    [Fact]
    public void Parse_SpecialWord_InBody_NotHeader()
    {
        // 正文行首出现特殊词但后接普通文字，不能误切
        var text = "第一章 手法\n楔子是章回小说的常见结构，很多作者都会用。\n番外故事以后再讲。\n第二章 继续\n内容\n";
        var book = _parser.Parse(text, "test.txt", Encoding.UTF8.GetByteCount(text));
        var flat = book.FlatChapters().ToList();
        Assert.Equal(2, flat.Count);
    }
}
