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

/// <summary>
/// Base class for Avalonia UI component tests providing headless testing infrastructure
/// </summary>
public abstract class TestBase : IDisposable
{
    private static Application? _staticApp;
    private static readonly object _lockObject = new object();
    private readonly Window _window;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    protected TestBase()
    {
        // Ensure Avalonia app is initialized only once
        lock (_lockObject)
        {
            if (_staticApp == null)
            {
                _staticApp = BuildAvaloniaApp();
            }
        }

        // Setup service container with required services
        _serviceProvider = BuildServiceProvider();

        // Create a test window on the UI thread
        _window = Dispatcher.UIThread.Invoke(() => new Window
        {
            Width = 800,
            Height = 600,
            Title = "Test Window"
        });

        // Don't show the window to avoid potential hang
        // Dispatcher.UIThread.Invoke(() => _window.Show());
    }

    /// <summary>
    /// Gets the service provider for dependency injection
    /// </summary>
    protected IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the test window for placing components
    /// </summary>
    protected Window Window => _window;

    /// <summary>
    /// Executes an action on the UI thread and waits for it to complete
    /// </summary>
    protected T RunOnUIThread<T>(Func<T> action)
    {
        return Dispatcher.UIThread.Invoke(action);
    }

    /// <summary>
    /// Executes an action on the UI thread and waits for it to complete
    /// </summary>
    protected void RunOnUIThread(Action action)
    {
        Dispatcher.UIThread.Invoke(action);
    }

    /// <summary>
    /// Executes an async action on the UI thread and waits for it to complete
    /// </summary>
    protected Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> action)
    {
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    /// Executes an async action on the UI thread and waits for it to complete
    /// </summary>
    protected Task RunOnUIThreadAsync(Func<Task> action)
    {
        return Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    /// Creates a UserControl of the specified type with dependency injection
    /// </summary>
    protected T CreateControl<T>() where T : UserControl
    {
        return RunOnUIThread(() => (T)ActivatorUtilities.CreateInstance<T>(_serviceProvider));
    }

    /// <summary>
    /// Places a control in the test window and returns it
    /// </summary>
    protected T PlaceInWindow<T>(T control) where T : Control
    {
        RunOnUIThread(() =>
        {
            _window.Content = control;
        });
        return control;
    }

    /// <summary>
    /// Waits for the UI to settle (processes all pending operations)
    /// </summary>
    protected async Task WaitForUIAsync()
    {
        await RunOnUIThreadAsync(async () =>
        {
            // Process all pending operations
            await Task.Delay(10); // Small delay to allow pending operations
        });
    }

    /// <summary>
    /// Builds the Avalonia application for headless testing
    /// </summary>
    private static Application BuildAvaloniaApp()
    {
        // Check if Application is already initialized
        if (Application.Current != null)
        {
            return Application.Current;
        }

        var app = AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true  // Change to true for pure headless
            })
            .SetupWithoutStarting();

        return Application.Current ?? new Application();
    }

    /// <summary>
    /// Builds the service provider with all required dependencies
    /// </summary>
    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Copy service registration from Avalonia Demo
        // Encryption strategy (desktop)
        services.AddSingleton<Nethereum.Wallet.IEncryptionStrategy, Nethereum.Wallet.DefaultAes256EncryptionStrategy>();

        // File-based Vault (use temporary path for tests)
        services.AddSingleton<Nethereum.Wallet.IWalletVaultService>(sp =>
        {
            var tempDir = System.IO.Path.GetTempPath();
            var testDir = System.IO.Path.Combine(tempDir, "NethereumTests", Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(testDir);
            var filePath = System.IO.Path.Combine(testDir, "test-vault.json");
            return new Nethereum.Wallet.FileWalletVaultService(filePath, sp.GetRequiredService<Nethereum.Wallet.IEncryptionStrategy>());
        });

        // Minimal configuration for tests - comment out complex services that cause issues

        // Core wallet account service
        services.AddSingleton<Nethereum.Wallet.ICoreWalletAccountService>(sp =>
        {
            var vaultService = sp.GetRequiredService<Nethereum.Wallet.IWalletVaultService>();
            var encryptionStrategy = sp.GetRequiredService<Nethereum.Wallet.IEncryptionStrategy>();
            var vault = vaultService.GetCurrentVault() ?? new Nethereum.Wallet.WalletVault(encryptionStrategy);
            return new Nethereum.Wallet.CoreWalletAccountService(vault);
        });

        // Host provider (adds RpcHandlerRegistry)
        services.AddNethereumWalletHostProvider();
        services.AddScoped<Nethereum.UI.SelectedEthereumHostProviderService>();

        // UI configuration
        services.AddNethereumWalletUIConfiguration(config =>
        {
            config.ApplicationName = "Nethereum Tests";
            config.WalletConfig.Security.MinPasswordLength = 8;
            config.WalletConfig.Behavior.EnableWalletReset = true;
            config.WalletConfig.AllowPasswordVisibilityToggle = true;
        });

        // Avalonia wallet UI + notification services
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
                // Ignore disposal errors
            }

            // Application doesn't have Dispose method in this version
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}