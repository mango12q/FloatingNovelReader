using System;
using System.Collections.Generic;
using System.Threading;

namespace FloatingNovelReader.Core;

/// <summary>
/// IEventAggregator 的默认实现。基于 Dictionary&lt;Type, List&lt;Delegate&gt;&gt; 分发事件。
/// 线程安全，异常隔离。
/// </summary>
/// <typeparam name="TEventBase">所有事件的公共标记接口</typeparam>
public sealed class EventAggregator<TEventBase> : IEventAggregator<TEventBase>
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Publish<TEvent>(TEvent @event) where TEvent : TEventBase
    {
        List<Delegate>? snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
            snapshot = new List<Delegate>(list);
        }
        foreach (var handler in snapshot)
        {
            try { ((Action<TEvent>)handler)(@event); }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "EventAggregator 分发 {EventType} 时异常", typeof(TEvent).Name);
            }
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : TEventBase
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                _handlers[typeof(TEvent)] = list = new List<Delegate>();
            list.Add(handler);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : TEventBase
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
                list.Remove(handler);
        }
    }
}
