using System;
using Nethereum.Wallet.UI.Components.Abstractions;

namespace Nethereum.Wallet.UI.Components.Avalonia.Services
{
    public interface INotifyWalletLoadingService : IWalletLoadingService
    {
        event Action<bool, string?> LoadingStateChanged;
        event Action<double, string?> ProgressChanged;
    }
}
