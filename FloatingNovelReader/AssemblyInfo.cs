using System.Runtime.CompilerServices;
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]

// 单测要覆盖解析器的内部纯函数（换行重排、href 解析、章节切分等），
// 这些细节不适合暴露成公开 API。
[assembly: InternalsVisibleTo("FloatingNovelReader.Tests")]
