using System;
using System.Collections.Generic;
using System.Threading;

namespace FloatingNovelReader.Core;

/// <summary>
/// 强类型事件聚合器。替代基于字符串的 EventBus，提供编译时类型安全。
/// </summary>
/// <typeparam name="TEventBase">所有事件的公共基接口（空接口即可）</typeparam>
public interface IEventAggregator<in TEventBase>
{
    /// <summary>发布事件（同步调用所有订阅者）</summary>
    void Publish<TEvent>(TEvent @event) where TEvent : TEventBase;

    /// <summary>订阅事件</summary>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : TEventBase;

    /// <summary>取消订阅</summary>
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : TEventBase;
}
