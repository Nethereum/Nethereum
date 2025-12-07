using System;
using System.Threading.Tasks;
using Nethereum.Wallet.Diagnostics;

namespace Nethereum.Wallet.UI.Components.Blazor.Services;

/// <summary>
/// Provides a bridge for services resolved outside the Blazor component tree to
/// execute work on the wallet overlay's UI dispatcher when needed (e.g. Mud dialogs).
/// </summary>
public static class WalletBlazorDispatcher
{
    private static readonly object Sync = new();
    private static Func<Func<Task>, Task>? _dispatcher;

    public static void Register(Func<Func<Task>, Task> dispatcher)
    {
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));
        lock (Sync)
        {
            _dispatcher = dispatcher;
        }
        WalletDiagnosticsLogger.Log("BlazorDispatcher", $"Dispatcher registered on thread {Environment.CurrentManagedThreadId}");
    }

    public static void Unregister(Func<Func<Task>, Task> dispatcher)
    {
        if (dispatcher == null) return;

        lock (Sync)
        {
            if (_dispatcher == dispatcher)
            {
                _dispatcher = null;
            }
        }
        WalletDiagnosticsLogger.Log("BlazorDispatcher", $"Dispatcher unregistered on thread {Environment.CurrentManagedThreadId}");
    }

    public static Task RunAsync(Func<Task> action)
    {
        var dispatcher = GetDispatcher();
        if (dispatcher != null)
        {
            return dispatcher(action);
        }

        WalletDiagnosticsLogger.Log("BlazorDispatcher", "RunAsync invoked without dispatcher; executing inline");
        return action();
    }

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = GetDispatcher();
        if (dispatcher == null)
        {
            WalletDiagnosticsLogger.Log("BlazorDispatcher", "RunAsync<T> invoked without dispatcher; executing inline");
            return await action().ConfigureAwait(false);
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        await dispatcher(async () =>
        {
            try
            {
                var result = await action().ConfigureAwait(false);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }).ConfigureAwait(false);

        return await tcs.Task.ConfigureAwait(false);
    }

    private static Func<Func<Task>, Task>? GetDispatcher()
    {
        lock (Sync)
        {
            return _dispatcher;
        }
    }
}
