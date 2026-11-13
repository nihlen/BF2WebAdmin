using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Common;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>The modules' registration in the host (<see cref="ModuleServices"/>) and a server's scope.</summary>
public class ModuleServicesTests
{
    private static ServiceProvider BuildHost()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BF2DB"] = "Data Source=:memory:",
            ["Database:Provider"] = "sqlite",
        }).Build();

        // As Program.cs registers them
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>());
        services.AddSignalR();
        DatabaseSetup.FromConfiguration(configuration).AddRepositories(services, maps: true, joins: true);
        services.AddGameModules(configuration);

        // What a Development host checks: every module can be built, and nothing scoped comes from the root
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void EveryModuleCanBeBuilt_AndEachServerScopeHasItsOwn()
    {
        using var host = BuildHost();

        using var scope1 = host.CreateScope();
        using var scope2 = host.CreateScope();
        var server1 = Substitute.For<IGameServer>();
        var server2 = Substitute.For<IGameServer>();
        scope1.ServiceProvider.GetRequiredService<ModuleScope>().Initialize(server1, Substitute.For<IMediator>(), Substitute.For<ITaskRunner>(), new CancellationTokenSource());
        scope2.ServiceProvider.GetRequiredService<ModuleScope>().Initialize(server2, Substitute.For<IMediator>(), Substitute.For<ITaskRunner>(), new CancellationTokenSource());

        var jail1 = scope1.ServiceProvider.GetRequiredService<JailModule>();
        var jail2 = scope2.ServiceProvider.GetRequiredService<JailModule>();
        Assert.NotSame(jail1, jail2);
        Assert.Same(jail1, scope1.ServiceProvider.GetRequiredService<JailModule>());
        Assert.Same(server1, scope1.ServiceProvider.GetRequiredService<IGameServer>());
        Assert.Same(server2, scope2.ServiceProvider.GetRequiredService<IGameServer>());

        // Shared by both
        Assert.Same(scope1.ServiceProvider.GetRequiredService<IDelayProvider>(), scope2.ServiceProvider.GetRequiredService<IDelayProvider>());
    }

    [Fact]
    public void AModuleOutsideAServerScope_FailsClearly()
    {
        using var host = BuildHost();
        using var scope = host.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<JailModule>());
        Assert.Contains("server's scope", error.Message);
        Assert.Throws<InvalidOperationException>(() => host.GetRequiredService<JailModule>());
    }
}
