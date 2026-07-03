using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using FloatingNovelReader.Core;
using FloatingNovelReader.Models;
using Serilog;

namespace FloatingNovelReader.Services;

/// <summary>
/// 自动阅读：定时触发翻页，可调速。
/// </summary>
public sealed class AutoReadService
{
    private readonly DispatcherTimer _timer;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public event EventHandler? Tick;
    public event EventHandler? Started;
    public event EventHandler? Stopped;

    public int IntervalSec
    {
        get => _intervalSec;
        set
        {
            _intervalSec = Math.Clamp(value, Constants.MinAutoReadIntervalSec, Constants.MaxAutoReadIntervalSec);
            _timer.Interval = TimeSpan.FromSeconds(_intervalSec);
            EventBus.Default.Publish(Constants.EvtAutoReadIntervalChanged, _intervalSec);
        }
    }
    private int _intervalSec = Constants.DefaultAutoReadIntervalSec;

    public AutoReadService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_intervalSec) };
        _timer.Tick += (s, e) => Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _timer.Interval = TimeSpan.FromSeconds(_intervalSec);
        _timer.Start();
        EventBus.Default.Publish(Constants.EvtReadingModeChanged, ReadingMode.AutoRead);
        Started?.Invoke(this, EventArgs.Empty);
        Log.Information("自动阅读已启动, 间隔 {Sec}s", _intervalSec);
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _timer.Stop();
        EventBus.Default.Publish(Constants.EvtReadingModeChanged, ReadingMode.Manual);
        Stopped?.Invoke(this, EventArgs.Empty);
        Log.Information("自动阅读已停止");
    }

    public void Toggle()
    {
        if (_isRunning) Stop();
        else Start();
    }

    public void Faster()
    {
        IntervalSec = Math.Max(Constants.MinAutoReadIntervalSec, _intervalSec - 1);
        if (_isRunning) _timer.Interval = TimeSpan.FromSeconds(_intervalSec);
    }

    public void Slower()
    {
        IntervalSec = Math.Min(Constants.MaxAutoReadIntervalSec, _intervalSec + 1);
        if (_isRunning) _timer.Interval = TimeSpan.FromSeconds(_intervalSec);
    }
}
