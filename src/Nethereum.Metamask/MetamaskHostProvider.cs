using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.UI;
using Nethereum.Web3;

namespace Nethereum.Metamask
{
    public class MetamaskHostProvider: IEthereumHostProvider
    {
        private readonly IMetamaskInterop _metamaskInterop;
        public static MetamaskHostProvider Current { get; private set; }
        public string Name { get; } = "Metamask";
        public bool Available { get; private set; }
        public string SelectedAccount { get; private set; }
        public long SelectedNetworkChainId { get; private set; }
        public bool Enabled { get; private set; }
        public IClient Client { get; }

        private MetamaskInterceptor _metamaskInterceptor;

        public event Func<string, Task> SelectedAccountChanged;
        public event Func<long, Task> NetworkChanged;
        public event Func<bool, Task> AvailabilityChanged;
        public event Func<bool, Task> EnabledChanged;
        public async Task<bool> CheckProviderAvailabilityAsync()
        {
            var result = await _metamaskInterop.CheckMetamaskAvailability().ConfigureAwait(false);
            await ChangeMetamaskAvailableAsync(result).ConfigureAwait(false);
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
            web3.Client.OverridingRequestInterceptor = _metamaskInterceptor;
            return Task.FromResult(web3);
        }

        public async Task<string> EnableProviderAsync()
        {
            var selectedAccount = await _metamaskInterop.EnableEthereumAsync().ConfigureAwait(false);
            Enabled = !string.IsNullOrEmpty(selectedAccount);

            if (Enabled)
            {
                await ChangeMetamaskEnabledAsync(true).ConfigureAwait(false);
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
            var result = await _metamaskInterop.GetSelectedAddress().ConfigureAwait(false);
            await ChangeSelectedAccountAsync(result).ConfigureAwait(false);
            return result;
        }

        public async Task<string> SignMessageAsync(string message)
        {
            return await _metamaskInterop.SignAsync(message.ToHexUTF8()).ConfigureAwait(false);
        }

        public MetamaskHostProvider(IMetamaskInterop metamaskInterop, IClient client = null, bool useOnlySigningWalletTransactionMethods = false)
        {
            _metamaskInterop = metamaskInterop;
            Client = client;
            _metamaskInterceptor = new MetamaskInterceptor(_metamaskInterop, this, useOnlySigningWalletTransactionMethods);
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

        public async Task ChangeMetamaskAvailableAsync(bool available)
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

        public async Task ChangeMetamaskEnabledAsync(bool enabled)
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
