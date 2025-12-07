using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Chain;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Wallet;
using Nethereum.UI;
using Nethereum.Wallet.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
    {
        public interface IWalletContext : IEthereumHostProvider
        {
        IReadOnlyList<IWalletAccount> Accounts { get; }
        IWalletAccount? SelectedWalletAccount { get; }
        DappConnectionContext? SelectedDapp { get; set; }
        IDappPermissionService DappPermissions { get; }

        Task<IAccount?> GetSelectedAccountAsync();

        // Hex representation convenience (derived from SelectedNetworkChainId).
        HexBigInteger? ChainId { get; }

        // Optional low-level access (providers may return null until enabled).
        Task<IClient?> GetRpcClientAsync();

        Task<IWeb3> GetWalletWeb3Async();

        // Chain switching via hex chainId (e.g. "0x1").
        Task<bool> SwitchChainAsync(string chainIdHex);

        // UI prompt + sign + (optionally) send transaction; returns tx hash or null if rejected.
        Task<string?> ShowTransactionDialogAsync(TransactionInput input);

        // Message / arbitrary data signing prompt.
        Task<string?> RequestPersonalSignAsync(SignaturePromptContext context);
        Task<string?> RequestTypedDataSignAsync(TypedDataSignPromptContext context);

        // Request permission for the current dapp to access the selected account.
        Task<bool> RequestDappPermissionAsync(DappConnectionContext dappContext, string accountAddress);
        Task<ChainAdditionPromptResult> RequestChainAdditionAsync(ChainAdditionPromptRequest request);
        Task<ChainSwitchPromptResult> RequestChainSwitchAsync(ChainSwitchPromptRequest request);

        // Add (or update) chain metadata to configuration.
        Task AddChainAsync(ChainFeature chainMetadata);

        // Initialise internal account collection (typically after vault unlock).
        void Initialise(IReadOnlyList<IWalletAccount> accounts, IWalletAccount? selected);

        // Mutate selected wallet account (fires provider events).
        Task SetSelectedWalletAccountAsync(IWalletAccount? account);

        // Convenience address-based selection (case‑insensitive).
        void SetSelectedAccount(string account);
        Task InitialiseAccountSignerAsync();

        IWalletConfigurationService Configuration { get; }
    }
}
