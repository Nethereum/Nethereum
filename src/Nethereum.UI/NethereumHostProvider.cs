using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System;
using System.Threading.Tasks;

namespace Nethereum.UI
{
    public class NethereumHostProvider : IEthereumHostProvider
    {
        public string Name => "Nethereum Host Provider";

        public bool Available => true;

        protected Account Account { get; private set; }

        protected string Url { get; set; }

        public string SelectedAccount { get { return Account == null ? null : Account.Address; } }

        public bool Enabled => Account != null && !string.IsNullOrEmpty(Url);

        public long SelectedNetworkChainId { get; private set; }

        public event Func<string, Task> SelectedAccountChanged;
        public event Func<long, Task> NetworkChanged;
        public event Func<bool, Task> AvailabilityChanged;
        public event Func<bool, Task> EnabledChanged;

        public async Task<bool> CheckProviderAvailabilityAsync()
        {
            await ChangeAvailableAsync(true);
            return await Task.FromResult(true);
        }

        public async Task ChangeAvailableAsync(bool available)
        {
            if (AvailabilityChanged != null)
            {
                await AvailabilityChanged.Invoke(available).ConfigureAwait(false);
            }
            
        }

        public Task<string> EnableProviderAsync()
        {
            return Task.FromResult(SelectedAccount);
        }

        public Task<string> GetProviderSelectedAccountAsync()
        {
            return Task.FromResult(SelectedAccount);
        }

        public Task<long> GetProviderSelectedNetworkAsync()
        {
            return Task.FromResult(SelectedNetworkChainId);
        }

        public Task<string> SignMessageAsync(string message)
        {
            var signer = new EthereumMessageSigner();
            var signedMessage = signer.EncodeUTF8AndSign(message, new EthECKey(((Account)Account).PrivateKey));
            return Task.FromResult(signedMessage);
        }

        public void SetSelectedAccount(string privateKey)
        { 
            SetSelectedAccount(new Account(privateKey, SelectedNetworkChainId));
        }

        public void SetSelectedAccount(Account account)
        {
            Account = account;

            if (SelectedAccountChanged != null)
            {
                if (Account != null)
                {

                    SelectedAccountChanged(Account.Address);
                }
                else
                {
                    SelectedAccountChanged(null);
                }
            }
        }

        public async Task<bool> SetUrl(string url)
        {
            var web3 = new Web3.Web3(url);
            try
            {
                var chainId = await web3.Eth.ChainId.SendRequestAsync();
                Url = url;
                SelectedNetworkChainId = (long)chainId.Value;

                if (NetworkChanged != null)
                {
                    await NetworkChanged(SelectedNetworkChainId);
                }
                return true;
            }
            catch 
            {
                Url = null;
                return false;
            }
        }

        public Task<IWeb3> GetWeb3Async()
        {
            return Task.FromResult((IWeb3)new Web3.Web3(new Account(Account.PrivateKey, SelectedNetworkChainId), Url));
        }
    }
}