using System.Collections.Generic;
using System.Linq;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 声音目录的解析与排序（纯函数部分）。
/// 联网部分不测（避免测试依赖外网）；拿真实接口返回的 JSON 片段验证字段提取。
/// </summary>
public class TtsVoiceCatalogTests
{
    // 形状与 edge 声音接口一致（只保留用到的字段）
    private const string SampleJson = """
    [
      { "Name": "Microsoft Server Speech Text to Speech Voice (en-US, AriaNeural)",
        "ShortName": "en-US-AriaNeural", "Gender": "Female", "Locale": "en-US" },
      { "ShortName": "zh-CN-YunxiNeural", "Gender": "Male", "Locale": "zh-CN" },
      { "ShortName": "zh-CN-XiaoxiaoNeural", "Gender": "Female", "Locale": "zh-CN" },
      { "ShortName": "ja-JP-NanamiNeural", "Gender": "Female", "Locale": "ja-JP" }
    ]
    """;

    [Fact]
    public void ParseVoices_ExtractsShortNameLocaleGender()
    {
        var voices = TtsVoiceCatalog.ParseVoices(SampleJson);

        Assert.Equal(4, voices.Count);
        var aria = voices.Single(v => v.ShortName == "en-US-AriaNeural");
        Assert.Equal("en-US", aria.Locale);
        Assert.Equal("Female", aria.Gender);
    }

    [Fact]
    public void ParseVoices_SkipsElementsWithoutShortName()
    {
        const string json = """
        [
          { "ShortName": "zh-CN-XiaoxiaoNeural", "Locale": "zh-CN" },
          { "Name": "no short name here" },
          { "ShortName": "" },
          "a bare string",
          { "ShortName": "zh-CN-YunxiNeural", "Locale": "zh-CN" }
        ]
        """;

        var voices = TtsVoiceCatalog.ParseVoices(json);

        Assert.Equal(2, voices.Count);
    }

    [Fact]
    public void ParseVoices_MissingOptionalFields_YieldsEmptyStrings()
    {
        var voices = TtsVoiceCatalog.ParseVoices("""[{"ShortName":"x-XX-A"}]""");

        var voice = Assert.Single(voices);
        Assert.Equal(string.Empty, voice.Locale);
        Assert.Equal(string.Empty, voice.Gender);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("\"just a string\"")]
    [InlineData("{\"voices\":[]}")]   // 不是数组
    public void ParseVoices_MalformedInput_ReturnsEmptyWithoutThrowing(string json)
    {
        Assert.Empty(TtsVoiceCatalog.ParseVoices(json));
    }

    [Fact]
    public void ParseVoices_Null_ReturnsEmpty()
    {
        Assert.Empty(TtsVoiceCatalog.ParseVoices(null));
    }

    [Fact]
    public void OrderVoices_PutsChineseFirst()
    {
        var voices = TtsVoiceCatalog.ParseVoices(SampleJson);

        var ordered = TtsVoiceCatalog.OrderVoices(voices);

        Assert.Equal("zh-CN-XiaoxiaoNeural", ordered[0].ShortName);
        Assert.Equal("zh-CN-YunxiNeural", ordered[1].ShortName);
        // 非中文排在后面
        Assert.Contains(ordered.Skip(2), v => v.Locale == "en-US");
        Assert.Contains(ordered.Skip(2), v => v.Locale == "ja-JP");
    }

    [Fact]
    public void OrderVoices_DeduplicatesByShortName()
    {
        var voices = new List<TtsVoice>
        {
            new("zh-CN-XiaoxiaoNeural", "zh-CN", "Female"),
            new("zh-CN-XiaoxiaoNeural", "zh-CN", "Female"),
            new("en-US-AriaNeural", "en-US", "Female"),
        };

        Assert.Equal(2, TtsVoiceCatalog.OrderVoices(voices).Count);
    }

    [Fact]
    public void OrderVoices_IsStableForEmptyInput()
    {
        Assert.Empty(TtsVoiceCatalog.OrderVoices(new List<TtsVoice>()));
    }
}
