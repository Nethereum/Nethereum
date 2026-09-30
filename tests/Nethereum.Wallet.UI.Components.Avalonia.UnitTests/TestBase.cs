using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet.UI.Components.Transactions;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Hosting;
using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests;

public abstract class TestBase : IDisposable
{
    private static Application? _staticApp;
    private static readonly object _lockObject = new object();
    private readonly Window _window;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    protected TestBase()
    {
        lock (_lockObject)
        {
            if (_staticApp == null)
            {
                _staticApp = BuildAvaloniaApp();
            }
        }

        _serviceProvider = BuildServiceProvider();

        _window = Dispatcher.UIThread.Invoke(() => new Window
        {
            Width = 800,
            Height = 600,
            Title = "Test Window"
        });

    }

    protected IServiceProvider ServiceProvider => _serviceProvider;

    protected Window Window => _window;

    protected T RunOnUIThread<T>(Func<T> action)
    {
        return Dispatcher.UIThread.Invoke(action);
    }

    protected void RunOnUIThread(Action action)
    {
        Dispatcher.UIThread.Invoke(action);
    }

    protected Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> action)
    {
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    protected Task RunOnUIThreadAsync(Func<Task> action)
    {
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    protected T CreateControl<T>() where T : UserControl
    {
        return RunOnUIThread(() => (T)ActivatorUtilities.CreateInstance<T>(_serviceProvider));
    }

    protected T PlaceInWindow<T>(T control) where T : Control
    {
        RunOnUIThread(() =>
        {
            _window.Content = control;
        });
        return control;
    }

    protected async Task WaitForUIAsync()
    {
        await RunOnUIThreadAsync(async () =>
        {
            await Task.Delay(10);
        });
    }

    private static Application BuildAvaloniaApp()
    {
        if (Application.Current != null)
        {
            return Application.Current;
        }

        var app = AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            })
            .SetupWithoutStarting();

        return Application.Current ?? new Application();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<Nethereum.Wallet.IEncryptionStrategy, Nethereum.Wallet.DefaultAes256EncryptionStrategy>();

        services.AddSingleton<Nethereum.Wallet.IWalletVaultService>(sp =>
        {
            var tempDir = System.IO.Path.GetTempPath();
            var testDir = System.IO.Path.Combine(tempDir, "NethereumTests", Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(testDir);
            var filePath = System.IO.Path.Combine(testDir, "test-vault.json");
            return new Nethereum.Wallet.FileWalletVaultService(filePath, sp.GetRequiredService<Nethereum.Wallet.IEncryptionStrategy>());
        });


        services.AddSingleton<Nethereum.Wallet.ICoreWalletAccountService>(sp =>
        {
            var vaultService = sp.GetRequiredService<Nethereum.Wallet.IWalletVaultService>();
            var encryptionStrategy = sp.GetRequiredService<Nethereum.Wallet.IEncryptionStrategy>();
            var vault = vaultService.GetCurrentVault() ?? new Nethereum.Wallet.WalletVault(encryptionStrategy);
            return new Nethereum.Wallet.CoreWalletAccountService(vault);
        });

        services.AddNethereumWalletHostProvider();
        services.AddScoped<Nethereum.UI.SelectedEthereumHostProviderService>();

        services.AddNethereumWalletUIConfiguration(config =>
        {
            config.ApplicationName = "Nethereum Tests";
            config.WalletConfig.Security.MinPasswordLength = 8;
            config.WalletConfig.Behavior.EnableWalletReset = true;
            config.WalletConfig.AllowPasswordVisibilityToggle = true;
        });

        services.AddNethereumWalletAvaloniaComponents();
        services.AddSingleton<Nethereum.Wallet.UI.Components.Abstractions.IWalletNotificationService,
            Nethereum.Wallet.UI.Components.Avalonia.Services.AvaloniaWalletNotificationService>();

        return services.BuildServiceProvider();
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _window?.Close();
                });
            }
            catch
            {
            }

            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}