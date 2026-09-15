using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FloatingNovelReader.Tests.Build;

/// <summary>
/// 发布脚本的编码守卫。
///
/// <c>Build/build.ps1</c> 里全是中文注释与中文字符串，而它是 **UTF-8 无 BOM** 时会坏：
/// Windows PowerShell 5.1（中文 Windows 的默认 shell）按系统 ANSI（GBK）读无 BOM 脚本，
/// 中文被解错后字节会破坏引号配对，整个脚本直接报
/// <c>Unexpected token ... Missing closing '}'</c> 而根本无法执行。
///
/// 这个坑很隐蔽：用 PS7 或开了「UTF-8 全球语言支持」的机器上一切正常，
/// 而在普通中文 Windows 上直接不可用；而且**很多编辑器/工具保存时会顺手去掉 BOM**。
/// 所以用测试把它钉住。
/// </summary>
public class BuildScriptEncodingTests
{
    private static string ScriptPath => Path.Combine(AppContext.BaseDirectory, "Build", "build.ps1");

    [Fact]
    public void BuildScript_HasUtf8Bom()
    {
        Assert.True(File.Exists(ScriptPath),
            $"未找到脚本副本: {ScriptPath}（检查 Tests.csproj 里 Build/build.ps1 的 CopyToOutputDirectory）");

        var bytes = File.ReadAllBytes(ScriptPath);

        Assert.True(bytes.Length >= 3, "脚本文件异常短");
        Assert.True(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "Build/build.ps1 缺少 UTF-8 BOM —— PowerShell 5.1 会按 ANSI(GBK) 读取，" +
            "中文注释会破坏语法导致脚本无法运行。请在保存时保留 BOM。");
    }

    [Fact]
    public void BuildScript_ContainsNoUtf8ReplacementCharacters()
    {
        // 另一个常见退化：以 GBK 保存过一次，中文变成不可恢复的替换字符
        var text = File.ReadAllText(ScriptPath, System.Text.Encoding.UTF8);

        Assert.DoesNotContain('\uFFFD', text);
    }

    [Fact]
    public void BuildScript_DeclaresTheExactPortableArtifactName()
    {
        // 产物名必须与 README 下载表、GitHub Release 附件名一致（不再带时间戳），
        // 否则发布时又得手动改名
        var text = File.ReadAllText(ScriptPath, System.Text.Encoding.UTF8);

        Assert.Contains("floating-novel-reader-portable-$Rid.zip", text);
        Assert.DoesNotContain("portable-$Rid-$timestamp.zip", text);
    }

    [Fact]
    public void ReadmeDownloadTable_MatchesTheBuiltArtifactNames()
    {
        // 三处必须一致：build.ps1 的产物名、README 的下载表、GitHub Release 的附件名
        var readme = Path.Combine(AppContext.BaseDirectory, "Build", "README.md");
        Assert.True(File.Exists(readme), $"未找到 README 副本: {readme}");

        var text = File.ReadAllText(readme, System.Text.Encoding.UTF8);

        Assert.Contains("| `floating-novel-reader-portable-win-x64.zip` |", text);
        Assert.DoesNotContain("| `floating-novel-reader-portable-win-x64-*.zip` |", text);
    }
}
