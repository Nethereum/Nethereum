using Nethereum.UI;
using Nethereum.Web3;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.Storage;
using System;
using System.Threading.Tasks;
using Nethereum.RPC.Accounts;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Chain;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.Services;

namespace Nethereum.Wallet.Hosting
{
    
    public class NethereumWalletHostProvider : IWalletContext
    {
        public string Name => "Nethereum Wallet";
        public bool Available { get; private set; } = true;

        public string SelectedAccount => _selectedAccount?.Address;
        public long SelectedNetworkChainId { get; private set; }
        public bool Enabled { get; private set; }

        public bool MultipleWalletsProvider => true;
        public bool MultipleWalletSelected => _accounts.Count > 1;

        // IWalletContext specific
        public IReadOnlyList<IWalletAccount> Accounts => _accounts;
        public IWalletAccount? SelectedWalletAccount => _selectedAccount;
        public HexBigInteger? ChainId => _activeChain != null ? new HexBigInteger(_activeChain.ChainId) : null;
        public IWalletConfigurationService Configuration => _configurationService;

        public event Func<string, Task> SelectedAccountChanged;
        public event Func<long, Task> NetworkChanged;
        public event Func<bool, Task> AvailabilityChanged;
        public event Func<bool, Task> EnabledChanged;

        private readonly IWalletVaultService _vaultService;
        private readonly ILoginPromptService _loginPromptService;          
        private readonly IRpcClientFactory _rpcClientFactory;
        private readonly IWalletStorageService _storageService;
        private readonly Services.Network.IChainManagementService _chainManagementService;
        private readonly RpcHandlerRegistry _handlerRegistry;
        private readonly ITransactionPromptService _transactionPromptService;
        private readonly ISignaturePromptService _signaturePromptService;
        private readonly IWalletConfigurationService _configurationService;

        private NethereumWalletInterceptor? _interceptor;
        private IWalletAccount? _selectedAccount;
        private readonly List<IWalletAccount> _accounts = new();
        private ChainFeature? _activeChain;
        private readonly long _defaultChainId;

        public NethereumWalletHostProvider(
            IWalletVaultService vaultService,
            IRpcClientFactory rpcClientFactory,
            IWalletStorageService storageService,
            Services.Network.IChainManagementService chainManagementService,
            RpcHandlerRegistry handlerRegistry,
            ITransactionPromptService transactionPromptService,
            ISignaturePromptService signaturePromptService,
            IWalletConfigurationService configurationService,
            ILoginPromptService loginPromptService,                   
            long defaultChainId = 1)
        {
            _vaultService = vaultService ?? throw new ArgumentNullException(nameof(vaultService));
            _rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _chainManagementService = chainManagementService ?? throw new ArgumentNullException(nameof(chainManagementService));
            _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
            _transactionPromptService = transactionPromptService ?? throw new ArgumentNullException(nameof(transactionPromptService));
            _signaturePromptService = signaturePromptService ?? throw new ArgumentNullException(nameof(signaturePromptService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _loginPromptService = loginPromptService ?? throw new ArgumentNullException(nameof(loginPromptService));
            _defaultChainId = defaultChainId;

            InitializeInterceptor();
            _ = InitializeActiveChainAsync();
        }

        private void InitializeInterceptor()
        {
            _interceptor = new NethereumWalletInterceptor(_handlerRegistry, this);
        }

        // IEthereumHostProvider
        public Task<bool> CheckProviderAvailabilityAsync() => Task.FromResult(Available);

        public async Task<Web3.IWeb3> GetWeb3Async()
        {
            if (_selectedAccount == null)
                throw new InvalidOperationException("No account selected. Please select an account first.");

            if (_activeChain == null)
                throw new InvalidOperationException("No chain selected. Please select a network first.");

            var client = await _rpcClientFactory.CreateClientAsync(_activeChain);
            var web3 = await _selectedAccount.CreateWeb3Async(client);

            if (_interceptor == null)
            {
                InitializeInterceptor();
            }

            _interceptor!.SetSelectedAccount(_selectedAccount.Address);

            web3.Client.OverridingRequestInterceptor = _interceptor;
            return (Web3.IWeb3)web3;
        }

        public async Task<string> EnableProviderAsync()
        {
            var vault = _vaultService.GetCurrentVault();
            if (vault == null)
            {
                var unlocked = await _loginPromptService.PromptLoginAsync();
                if (!unlocked)
                {
                    Enabled = false;
                    await RaiseEnabledChangedAsync(false);
                    return null;
                }
            }

            await RefreshAccountsAsync();

            if (_selectedAccount == null && _accounts.Any())
            {
                var storedAccountAddress = await _storageService.GetSelectedAccountAsync();
                var candidate = string.IsNullOrEmpty(storedAccountAddress)
                    ? _accounts.First()
                    : _accounts.FirstOrDefault(a => a.Address.Equals(storedAccountAddress, StringComparison.OrdinalIgnoreCase)) ?? _accounts.First();
                await SetSelectedWalletAccountAsync(candidate);
            }

            Enabled = true;
            await RaiseEnabledChangedAsync(true);
            return SelectedAccount;
        }

        public Task<string> GetProviderSelectedAccountAsync() => Task.FromResult(SelectedAccount);

        public async Task<string> SignMessageAsync(string message)
        {
            if (_selectedAccount == null)
                throw new InvalidOperationException("No account selected.");
            return await ShowSignPromptAsync(message);
        }

        // IWalletContext additions

        public Task<IAccount?> GetSelectedAccountAsync()
            => _selectedAccount == null ? Task.FromResult<IAccount?>(null) : _selectedAccount.GetAccountAsync();

        public async Task<IClient?> GetRpcClientAsync()
        {
            if (_activeChain == null) return null;
            return await _rpcClientFactory.CreateClientAsync(_activeChain);
        }

        public async Task<bool> SwitchChainAsync(string chainIdHex)
        {
            var bi = new HexBigInteger(chainIdHex).Value;
            return await SetSelectedNetworkAsync((long)bi);
        }

        public Task<string?> ShowTransactionDialogAsync(TransactionInput input)
            => _transactionPromptService.PromptTransactionAsync(input);

        public Task<string> ShowSignPromptAsync(string message)
            => _signaturePromptService.PromptSignatureAsync(message);

        public async Task AddChainAsync(ChainFeature chainMetadata)
        {
            if (chainMetadata == null) return;
            await _chainManagementService.AddCustomChainAsync(chainMetadata);
        }

        public void Initialise(IReadOnlyList<IWalletAccount> accounts, IWalletAccount? selected)
        {
            _accounts.Clear();
            if (accounts != null) _accounts.AddRange(accounts);
            if (selected != null && _accounts.Contains(selected))
            {
                _selectedAccount = selected;
                _selectedAccount.IsSelected = true;
            }
            else
            {
                _selectedAccount = _accounts.FirstOrDefault(a => a.IsSelected) ?? _accounts.FirstOrDefault();
                if (_selectedAccount != null) _selectedAccount.IsSelected = true;
            }
        }

        public async Task SetSelectedWalletAccountAsync(IWalletAccount? account)
        {
            if (account == null)
            {
                if (_selectedAccount != null)
                {
                    _selectedAccount.IsSelected = false;
                    _selectedAccount = null;
                    await RaiseSelectedAccountChangedAsync(null);
                }
                return;
            }

            if (_selectedAccount?.Address == account.Address) return;
            if (!_accounts.Contains(account)) return;

            if (_selectedAccount != null) _selectedAccount.IsSelected = false;

            _selectedAccount = account;
            _selectedAccount.IsSelected = true;

            await _storageService.SetSelectedAccountAsync(account.Address);
            _interceptor?.SetSelectedAccount(account.Address);

            await RaiseSelectedAccountChangedAsync(account.Address);
        }

        public void SetSelectedAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) return;
            var found = _accounts.FirstOrDefault(a => a.Address.Equals(account, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                _ = SetSelectedWalletAccountAsync(found);
            }
        }

        public async Task RefreshAccountsAsync()
        {
            var vaultAccounts = await _vaultService.GetAccountsAsync();
            var previous = _selectedAccount?.Address;

            _accounts.Clear();
            _accounts.AddRange(vaultAccounts);

            if (previous != null)
            {
                var still = _accounts.FirstOrDefault(a => a.Address.Equals(previous, StringComparison.OrdinalIgnoreCase));
                if (still != null)
                {
                    if (!ReferenceEquals(still, _selectedAccount))
                    {
                        if (_selectedAccount != null) _selectedAccount.IsSelected = false;
                        _selectedAccount = still;
                        _selectedAccount.IsSelected = true;
                        await RaiseSelectedAccountChangedAsync(_selectedAccount.Address);
                    }
                    return;
                }
            }

            var next = _accounts.FirstOrDefault(a => a.IsSelected) ?? _accounts.FirstOrDefault();
            if (next != null)
            {
                await SetSelectedWalletAccountAsync(next);
            }
            else
            {
                if (_selectedAccount != null)
                {
                    _selectedAccount = null;
                    await RaiseSelectedAccountChangedAsync(null);
                }
            }
        }

        public async Task<bool> SetSelectedAccountAsync(IWalletAccount account)
        {
            if (account == null) return false;
            if (!_accounts.Contains(account)) return false;
            await SetSelectedWalletAccountAsync(account);
            return _selectedAccount?.Address == account.Address;
        }

        public async Task<bool> SetSelectedNetworkAsync(long chainId)
        {
            try
            {
                var chainFeature = await _chainManagementService.GetChainAsync(new BigInteger(chainId));
                if (chainFeature == null)
                {
                    chainFeature = new ChainFeature
                    {
                        ChainId = new BigInteger(chainId),
                        ChainName = $"Chain {chainId}"
                    };
                }

                _activeChain = chainFeature;
                SelectedNetworkChainId = chainId;
                await _storageService.SetSelectedNetworkAsync(chainId);

                if (NetworkChanged != null)
                    await NetworkChanged.Invoke(chainId);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _chainManagementService.GetBestRpcEndpointAsync(chainFeature.ChainId);
                    }
                    catch { }
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task InitializeActiveChainAsync()
        {
            try
            {
                var selectedChainId = await _storageService.GetSelectedNetworkAsync();
                if (selectedChainId.HasValue)
                {
                    await SetSelectedNetworkAsync(selectedChainId.Value);
                }
                else
                {
                    await SetSelectedNetworkAsync(_defaultChainId);
                }
            }
            catch
            {
                SelectedNetworkChainId = _defaultChainId;
                _activeChain = new ChainFeature
                {
                    ChainId = new BigInteger(_defaultChainId),
                    ChainName = $"Chain {_defaultChainId}"
                };
            }
        }

        private async Task RaiseSelectedAccountChangedAsync(string? address)
        {
            if (SelectedAccountChanged != null)
                await SelectedAccountChanged.Invoke(address ?? string.Empty);
        }

        private async Task RaiseEnabledChangedAsync(bool enabled)
        {
            if (EnabledChanged != null)
                await EnabledChanged.Invoke(enabled);
        }

        public List<IWalletAccount> GetAccounts() => _accounts;
        public IWalletAccount GetSelectedAccount() => _selectedAccount;
        public bool IsWalletUnlocked() => _vaultService.GetCurrentVault() != null;
    }
}
