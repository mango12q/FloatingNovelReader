using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using FloatingNovelReader;
using FloatingNovelReader.Core;
using FloatingNovelReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 系统托盘。最小化时隐藏到托盘，右键菜单显示 / 书架 / 设置 / 退出。
/// 为了避免引入额外 NuGet 依赖，使用 System.Windows.Forms.NotifyIcon 实现托盘。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public void Initialize()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Constants.AppName,
            Visible = true,
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        var show = new System.Windows.Forms.ToolStripMenuItem("显示阅读窗口");
        show.Click += (s, e) => ShowReader();
        menu.Items.Add(show);

        var shelf = new System.Windows.Forms.ToolStripMenuItem("打开书架");
        shelf.Click += (s, e) => ShowBookshelf();
        menu.Items.Add(shelf);

        var settings = new System.Windows.Forms.ToolStripMenuItem("设置");
        settings.Click += (s, e) => ShowSettings();
        menu.Items.Add(settings);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var exit = new System.Windows.Forms.ToolStripMenuItem("退出");
        exit.Click += (s, e) => Exit();
        menu.Items.Add(exit);

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (s, e) => ShowReader();

        Log.Information("系统托盘已创建");
    }

    private static Icon LoadIcon()
    {
        try
        {
            // 尝试从嵌入资源加载
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FloatingNovelReader.Resources.Icons.app.ico");
            if (stream != null) return new Icon(stream);
        }
        catch
        {
            // 忽略
        }
        // 兜底：使用系统默认应用图标
        return SystemIcons.Application;
    }

    public void ShowReader()
    {
        var w = App.Services.GetRequiredService<ReaderWindow>();
        w.Show();
        w.Activate();
    }

    public void ShowBookshelf()
    {
        var w = App.Services.GetRequiredService<BookshelfWindow>();
        w.Show();
        w.Activate();
    }

    public void ShowSettings()
    {
        var w = App.Services.GetRequiredService<SettingsWindow>();
        w.ShowDialog();
    }

    public void Exit()
    {
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
