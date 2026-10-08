using System;
using System.Threading;
using System.Windows;
using FloatingNovelReader.Helpers;
using Xunit;
using Xunit.Sdk;

namespace FloatingNovelReader.Tests.Helpers;

/// <summary>
/// DpiHelper 的 WPF 粘合层（尺寸计算本身在 <see cref="DialogSizingTests"/> 里覆盖）。
/// 这里在 STA 线程上真的建两个 Window，验证写回 WPF 属性的结果自洽：
/// 位置模式被改成 Manual、尺寸等于计算结果、最小尺寸不会大于实际尺寸。
/// </summary>
public class DpiHelperTests
{
    [Fact]
    public void ApplyDialogLayout_OnStaThread_ProducesConsistentWindowGeometry()
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try
            {
                // owner 必须先 Show 过才能被设为 Owner（WPF 的限制）；
                // 放成屏幕外的 1×1 窗口，避免测试运行时闪一个可见窗口。
                var owner = new Window
                {
                    Width = 1,
                    Height = 1,
                    Left = -4000,
                    Top = -4000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStyle = WindowStyle.None,
                };
                owner.Show();

                var dialog = new Window { Owner = owner };

                var result = DpiHelper.ApplyDialogLayout(dialog, owner, 500, 700, 400, 500);

                Assert.Equal(WindowStartupLocation.Manual, dialog.WindowStartupLocation);
                Assert.Equal(result.Width, dialog.Width);
                Assert.Equal(result.Height, dialog.Height);
                Assert.Equal(result.Left, dialog.Left);
                Assert.Equal(result.Top, dialog.Top);

                // 最小尺寸不能大于实际尺寸，否则 WPF 会自己把窗口撑回去
                Assert.True(dialog.MinWidth <= dialog.Width);
                Assert.True(dialog.MinHeight <= dialog.Height);

                // 位置必须落在屏幕工作区内（跨 DPI 显示器时 WPF 的 CenterOwner 会算飞）
                var work = SystemParameters.WorkArea;
                Assert.True(dialog.Left >= work.Left - 1);
                Assert.True(dialog.Top >= work.Top - 1);
                Assert.True(dialog.Left + dialog.Width <= work.Right + 1);
                Assert.True(dialog.Top + dialog.Height <= work.Bottom + 1);

                // 拿不到 Win32 句柄时也不能算出 0 缩放
                Assert.True(DpiHelper.GetWindowScale(dialog) > 0);

                dialog.Close();
                owner.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                done.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(done.Wait(TimeSpan.FromSeconds(30)), "STA 线程未在 30 秒内完成");
        if (failure != null) throw new XunitException("STA 线程内断言失败：" + failure);
    }
}
