using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddEnterpriseDemoHostDeferred();
            var provider = services.BuildServiceProvider();

            var mainWindow = new MainWindow(provider);
            desktop.MainWindow = mainWindow;

            desktop.ShutdownRequested += (_, _) =>
            {
                var session = provider.GetRequiredService<SessionState>();
                session.InfraResource?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
