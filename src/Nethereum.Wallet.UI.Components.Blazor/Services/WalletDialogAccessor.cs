using MudBlazor;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public interface IWalletDialogAccessor
    {
        IDialogService? DialogService { get; set; }
    }

    public sealed class WalletDialogAccessor : IWalletDialogAccessor
    {
        public IDialogService? DialogService { get; set; }
    }
}
