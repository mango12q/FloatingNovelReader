using System;
using System.Collections.Generic;
using System.Threading;

namespace FloatingNovelReader.Core;

/// <summary>
/// 极简事件总线。解耦组件通信。线程安全。
/// 使用方式：
///   EventBus.Default.Subscribe("EventName", OnEvent);
///   EventBus.Default.Publish("EventName", payload);
/// </summary>
public sealed class EventBus
{
    public static EventBus Default { get; } = new EventBus();

    private readonly Dictionary<string, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe(string eventName, Action<object?> handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventName, out var list))
            {
                list = new List<Delegate>();
                _handlers[eventName] = list;
            }
            list.Add(handler);
        }
    }

    public void Subscribe<T>(string eventName, Action<T> handler)
    {
        Subscribe(eventName, (object? o) => handler((T)o!));
    }

    public void Unsubscribe(string eventName, Action<object?> handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventName, out var list))
            {
                list.Remove(handler);
            }
        }
    }

    public void Publish(string eventName, object? payload = null)
    {
        Delegate[] snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventName, out var list)) return;
            snapshot = list.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler.DynamicInvoke(payload);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "EventBus 分发事件 {Event} 时发生异常", eventName);
            }
        }
    }
}
