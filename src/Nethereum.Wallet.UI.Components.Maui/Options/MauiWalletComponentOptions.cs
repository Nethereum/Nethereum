using System;

namespace Nethereum.Wallet.UI.Components.Maui.Options
{
    public class MauiWalletComponentOptions
    {
        public long DefaultChainId { get; set; } = 1;
        public Action<Nethereum.Wallet.UI.Components.Configuration.NethereumWalletUIConfiguration>? ConfigureUi { get; set; }
    }
}
