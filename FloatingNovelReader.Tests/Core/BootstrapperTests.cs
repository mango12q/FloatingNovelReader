using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FloatingNovelReader.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloatingNovelReader.Tests.Core;

/// <summary>
/// DI 装配守卫。
///
/// 应用没有 UI 自动化测试，而"忘了注册某个接口"只会在运行时第一次解析时才抛异常
/// （本项目用的是 <c>BuildServiceProvider()</c> 而非 ValidateOnBuild）。
/// 这个测试在不实例化任何 WPF 窗口的前提下，检查每个实现类型的构造函数依赖都在容器里 ——
/// 正是本次重构最容易踩的一类错（新增了 IWindowNavigator / IDialogService / IPageAdvancer）。
/// </summary>
public class BootstrapperTests
{
    /// <summary>DI 容器自身永远可以提供这些。</summary>
    private static readonly HashSet<Type> AlwaysAvailable = new()
    {
        typeof(IServiceProvider),
        typeof(IServiceScopeFactory),
        typeof(IServiceProviderIsService),
    };

    [Fact]
    public void EveryConstructorDependencyIsRegistered()
    {
        var services = Bootstrapper.ConfigureServices(new ServiceCollection());
        var registered = services.Select(d => d.ServiceType).ToHashSet();

        var missing = new List<string>();

        foreach (var descriptor in services)
        {
            var impl = descriptor.ImplementationType;
            if (impl == null) continue;   // 工厂/实例注册（如 IPageAdvancer -> ReaderViewModel）由测试单独覆盖
            if (impl.IsAbstract || impl.IsInterface) continue;

            var ctors = impl.GetConstructors().ToList();
            if (ctors.Count == 0) continue;

            // DI 会挑"能全部满足"的构造函数；只要有一个构造函数可满足就算装配 OK。
            // （DatabaseService 就是这种：有 DatabaseService() 兜底构造 + DatabaseService(string) 测试用构造。）
            var unresolvable = ctors
                .Select(c => c.GetParameters()
                    .Where(p => !Satisfiable(p.ParameterType))
                    .Select(p => p.ParameterType.Name)
                    .ToList())
                .OrderBy(list => list.Count)
                .First();

            if (unresolvable.Count > 0)
            {
                missing.Add(
                    $"{impl.Name} 的任何一个构造函数都无法满足，缺少：{string.Join(", ", unresolvable)}");
            }
        }

        bool Satisfiable(Type t) => AlwaysAvailable.Contains(t) || registered.Contains(t);

        Assert.True(missing.Count == 0,
            "DI 装配不完整（运行时第一次解析才会抛异常）：" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Distinct()));
    }

    [Fact]
    public void PageAdvancer_ResolvesToTheSameInstanceAsReaderViewModel()
    {
        var provider = Bootstrapper.ConfigureServices(new ServiceCollection()).BuildServiceProvider();

        var vm = provider.GetRequiredService<FloatingNovelReader.ViewModels.ReaderViewModel>();
        var advancer = provider.GetRequiredService<IPageAdvancer>();

        // 自动阅读 / TTS 与阅读窗口必须操作同一个实例，否则翻页驱动的是另一个 VM
        Assert.Same(vm, advancer);
    }

    [Fact]
    public void WindowNavigatorAndDialogService_AreRegistered()
    {
        var registered = Bootstrapper.ConfigureServices(new ServiceCollection())
            .Select(d => d.ServiceType)
            .ToHashSet();

        Assert.Contains(typeof(IWindowNavigator), registered);
        Assert.Contains(typeof(IDialogService), registered);
        Assert.Contains(typeof(IPageAdvancer), registered);
    }
}
