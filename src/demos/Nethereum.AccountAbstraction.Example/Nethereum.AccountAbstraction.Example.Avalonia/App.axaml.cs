using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Example.Core;
using Nethereum.AccountAbstraction.Example.Hosting;
using Nethereum.WebAuthn.Windows;

namespace Nethereum.AccountAbstraction.Example.Avalonia;

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
            services.AddNethereumWebAuthnWindows();
            services.AddExampleHostDeferred();
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
