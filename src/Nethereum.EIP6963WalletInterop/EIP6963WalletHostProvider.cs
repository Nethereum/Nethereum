using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.UI;
using Nethereum.Web3;

namespace Nethereum.EIP6963WalletInterop
{
    public class EIP6963WalletHostProvider : IEthereumHostProvider
    {
        protected readonly IEIP6963WalletInterop _walletInterop;
        public static EIP6963WalletHostProvider Current { get; private set; }
        public string Name { get; } = "EIP6963 Standard";
        public bool MultipleWalletsProvider => true;
        public bool MultipleWalletSelected { get; private set; } = false;

        public bool Available { get; private set; }
        public string SelectedAccount
        {
            get => _selectedAccount; 
            private set
            {
                _selectedAccount = value;
                _walletInterceptor.SelectedAccount = value;
            }
        }
        public long SelectedNetworkChainId { get; private set; }
        public bool Enabled { get; private set; }
        public IClient Client { get; }

        private EIP6963WalletInterceptor _walletInterceptor;
        private string _selectedAccount;

        public event Func<string, Task> SelectedAccountChanged;
        public event Func<long, Task> NetworkChanged;
        public event Func<bool, Task> AvailabilityChanged;
        public event Func<bool, Task> EnabledChanged;

        public async Task<EIP6963WalletInfo[]> GetAvailableWalletsAsync()
        {
            return await _walletInterop.GetAvailableWalletsAsync();
        }

        public async Task SelectWalletAsync(string walletuuid)
        {
            await _walletInterop.SelectWalletAsync(walletuuid);
            MultipleWalletSelected = true;
            var result = await _walletInterop.GetSelectedAddress().ConfigureAwait(false);
            await ChangeSelectedAccountAsync(result).ConfigureAwait(false);
        }

        public async Task<bool> CheckProviderAvailabilityAsync()
        {
            var result = await _walletInterop.CheckAvailabilityAsync().ConfigureAwait(false);
            await ChangeWalletAvailableAsync(result).ConfigureAwait(false);
            return result;
        }


        public Task<Web3.IWeb3> GetWeb3Async()
        {
            IWeb3 web3 = null;
            if (Client == null)
            {
                web3 = new Web3.Web3();
            }
            else
            {
                web3 = new Web3.Web3(Client);
            }
            web3.Client.OverridingRequestInterceptor = _walletInterceptor;
            return Task.FromResult(web3);
        }

        public async Task<string> EnableProviderAsync()
        {
            var selectedAccount = await _walletInterop.EnableEthereumAsync().ConfigureAwait(false);
            Enabled = !string.IsNullOrEmpty(selectedAccount);

            if (Enabled)
            {
                await ChangeWalletEnabledAsync(true).ConfigureAwait(false);
                SelectedAccount = selectedAccount;
                if (SelectedAccountChanged != null)
                {
                    await SelectedAccountChanged.Invoke(selectedAccount).ConfigureAwait(false);
                }
                return selectedAccount;
            }

            return null;
        }

        public async Task<string> GetProviderSelectedAccountAsync()
        {
            var result = await _walletInterop.GetSelectedAddress().ConfigureAwait(false);
            await ChangeSelectedAccountAsync(result).ConfigureAwait(false);
            return result;
        }

        public async Task<string> SignMessageAsync(string message)
        {
            return await _walletInterop.SignAsync(message.ToHexUTF8()).ConfigureAwait(false);
        }

        public EIP6963WalletHostProvider(IEIP6963WalletInterop walletInterop, IClient client = null, bool useOnlySigningWalletTransactionMethods = false)
        {
            _walletInterop = walletInterop;
            Client = client;
            _walletInterceptor = new EIP6963WalletInterceptor(_walletInterop, useOnlySigningWalletTransactionMethods);
            Current = this;
        }

        public async Task ChangeSelectedAccountAsync(string selectedAccount)
        {
            if (SelectedAccount != selectedAccount)
            {
                SelectedAccount = selectedAccount;
                if (SelectedAccountChanged != null)
                {
                    await SelectedAccountChanged.Invoke(selectedAccount).ConfigureAwait(false);
                }
            }
        }

        public async Task ChangeSelectedNetworkAsync(long chainId)
        {
            if (SelectedNetworkChainId != chainId)
            {
                SelectedNetworkChainId = chainId;
                if (NetworkChanged != null)
                {
                    await NetworkChanged.Invoke(SelectedNetworkChainId).ConfigureAwait(false);
                }
            }
        }

        public async Task ChangeWalletAvailableAsync(bool available)
        {
            if (Available != available)
            {
                Available = available;
                if (AvailabilityChanged != null)
                {
                    await AvailabilityChanged.Invoke(available).ConfigureAwait(false);
                }
            }
        }

        public async Task ChangeWalletEnabledAsync(bool enabled)
        {
            if (Enabled != enabled)
            {
                Enabled = enabled;
                if (EnabledChanged != null)
                {
                    await EnabledChanged.Invoke(enabled).ConfigureAwait(false);
                }
            }
        }

    }
}
