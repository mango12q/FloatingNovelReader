using System;
using System.IO;
using System.Text;
using FloatingNovelReader.Helpers;
using FloatingNovelReader.Models;
using FloatingNovelReader.Services;
using Xunit;

namespace FloatingNovelReader.Tests.Services;

/// <summary>
/// 阅读会话的防抖刷盘契约。
///
/// 体检报告 P0「2.7 App.Shutdown 时不刷盘 → 最后 500ms 的进度丢失」的机制侧证明：
/// 翻页只标记脏并由 DispatcherTimer 延迟落库，所以**必须**有显式 Flush()；
/// App.OnExit 调用的就是它。这里直接断言"脏进度在 Flush 前未落库、Flush 后已落库"。
///
/// 测试线程没有消息循环，DispatcherTimer 不会触发，因此这个断言是确定性的（不靠时序）。
/// </summary>
public class ReadingSessionServiceTests : IDisposable
{
    private readonly string _dbFile;
    private readonly string _txtFile;
    private readonly DatabaseService _db;
    private readonly Book _book;

    public ReadingSessionServiceTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"fnr_session_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbFile);
        _db.Initialize();

        _txtFile = Path.Combine(Path.GetTempPath(), $"fnr_session_{Guid.NewGuid():N}.txt");
        File.WriteAllText(_txtFile,
            "第一章 开端\n内容A\n第二章 发展\n内容B\n第三章 高潮\n内容C\n",
            Encoding.UTF8);

        var importer = new BookImportService(_db, new ChapterParser());
        _book = importer.Import(_txtFile);
    }

    [Fact]
    public void Flush_PersistsPendingPage_ThatDebounceHasNotWrittenYet()
    {
        var session = new ReadingSessionService(_db);
        session.Open(_book);                 // 导入时已建了一行进度（第 0 页）

        session.SetPage(3);                  // 只标记脏：真机上要等 500ms 防抖

        // 关键断言：此刻库里仍是旧值 —— 证明"不 Flush 就真的会丢"
        Assert.Equal(0, _db.GetProgress(_book.Id)!.PageNumber);

        session.Flush();                     // App.OnExit 调用的就是这一句

        Assert.Equal(3, _db.GetProgress(_book.Id)!.PageNumber);
    }

    [Fact]
    public void Flush_WhenNothingPending_DoesNotThrowOrCorrupt()
    {
        var session = new ReadingSessionService(_db);
        session.Open(_book);

        session.Flush();
        session.Flush();

        Assert.NotNull(_db.GetProgress(_book.Id));
    }

    [Fact]
    public void Open_RestoresPreviouslyFlushedPage()
    {
        var first = new ReadingSessionService(_db);
        first.Open(_book);
        first.SetPage(2);
        first.Flush();

        // 新会话（等价于重启应用）应读回刚才刷进去的页码
        var second = new ReadingSessionService(_db);
        second.Open(_book);

        Assert.Equal(2, second.CurrentPage);
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try { if (File.Exists(_dbFile + suffix)) File.Delete(_dbFile + suffix); } catch { }
        }
        try { if (File.Exists(_txtFile)) File.Delete(_txtFile); } catch { }
    }
}
