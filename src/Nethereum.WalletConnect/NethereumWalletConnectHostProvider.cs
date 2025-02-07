using Nethereum.UI;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Nethereum.WalletConnect
{
    public class NethereumWalletConnectHostProvider : IEthereumHostProvider
    {
        public static NethereumWalletConnectHostProvider Current { get; private set; }
        public string Name { get; } = "WalletConnect";
        public bool Available { get; private set; }
        public bool MultipleWalletsProvider => false; //False for now
        public bool MultipleWalletSelected { get; private set; } = false;
        public string SelectedAccount
        {
            get => _selectedAccount;
            private set
            {
                _selectedAccount = value;
                _nethereumWalletConnectInterceptor.SelectedAccount = value;
            }
        }
        public long SelectedNetworkChainId { get; private set; }
        public bool Enabled { get; private set; } = true;
        public IClient Client { get; private set; }

        private NethereumWalletConnectInterceptor _nethereumWalletConnectInterceptor;
        private string _selectedAccount;
        private readonly NethereumWalletConnectService _walletConnectService;
        private readonly string _url;

        public event Func<string, Task> SelectedAccountChanged;
        public event Func<long, Task> NetworkChanged;
        public event Func<bool, Task> AvailabilityChanged;
        public event Func<bool, Task> EnabledChanged;
        public async Task<bool> CheckProviderAvailabilityAsync()
        {
            return true;
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
            web3.Client.OverridingRequestInterceptor = _nethereumWalletConnectInterceptor;
            return Task.FromResult(web3);
        }

        public async Task<string> EnableProviderAsync()
        { 
            Enabled = true;
            return null;
        }

        public async Task<string> GetProviderSelectedAccountAsync()
        {
            var result =  _walletConnectService.SelectedAccount;
            await ChangeSelectedAccountAsync(result).ConfigureAwait(false);
            return result;
        }

        public async Task<string> SignMessageAsync(string message)
        {
            return await _walletConnectService.SignAsync(message).ConfigureAwait(false);
        }

        public NethereumWalletConnectHostProvider(NethereumWalletConnectService walletConnectService, IClient client = null)
        {
            _walletConnectService = walletConnectService;
            Client = client;
            _nethereumWalletConnectInterceptor = new NethereumWalletConnectInterceptor(_walletConnectService);
            Current = this;
        }

        public NethereumWalletConnectHostProvider(NethereumWalletConnectService walletConnectService, string url, AuthenticationHeaderValue authHeaderValue = null,
            JsonSerializerSettings jsonSerializerSettings = null, HttpClientHandler httpClientHandler = null, ILogger log = null)
        {
            _walletConnectService = walletConnectService;
            InitNewClient(url, authHeaderValue, jsonSerializerSettings, httpClientHandler, log);
            _nethereumWalletConnectInterceptor = new NethereumWalletConnectInterceptor(_walletConnectService);
            Current = this;
        }

        public void InitNewClient(string url, AuthenticationHeaderValue authHeaderValue, JsonSerializerSettings jsonSerializerSettings, HttpClientHandler httpClientHandler, ILogger log)
        {
            Client = new RpcClient(new Uri(url), authHeaderValue, jsonSerializerSettings, httpClientHandler, log);
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

    }
}
