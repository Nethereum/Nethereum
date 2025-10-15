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
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Chain;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.Services;
using System.Text;

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
        public DappConnectionContext? SelectedDapp { get; set; }
        public IDappPermissionService DappPermissions => _dappPermissionService;
        public HexBigInteger? ChainId => _activeChain != null ? new HexBigInteger(_activeChain.ChainId) : null;
        public IWalletConfigurationService Configuration => _configurationService;

        public event Func<string, Task> SelectedAccountChanged;
        public event Func<long, Task> NetworkChanged;
        public event Func<bool, Task> AvailabilityChanged;
        public event Func<bool, Task> EnabledChanged;

        private readonly IWalletVaultService _vaultService;
        private readonly ILoginPromptService _loginPromptService;          
        private readonly IDappPermissionService _dappPermissionService;
        private readonly IRpcClientFactory _rpcClientFactory;
        private readonly IWalletStorageService _storageService;
        private readonly Services.Network.IChainManagementService _chainManagementService;
        private readonly RpcHandlerRegistry _handlerRegistry;
        private readonly ITransactionPromptService _transactionPromptService;
        private readonly ISignaturePromptService _signaturePromptService;
        private readonly IDappPermissionPromptService _dappPermissionPromptService;
        private readonly IChainAdditionPromptService _chainAdditionPromptService;
        private readonly IChainSwitchPromptService _chainSwitchPromptService;
        private readonly Guid _instanceId = Guid.NewGuid();
        public Guid InstanceId => _instanceId;
        private readonly IWalletConfigurationService _configurationService;

        public ChainFeature? ActiveChain => _activeChain;

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
            IDappPermissionService dappPermissionService,
            IDappPermissionPromptService dappPermissionPromptService,
            IChainAdditionPromptService chainAdditionPromptService,
            IChainSwitchPromptService chainSwitchPromptService,
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
            _dappPermissionService = dappPermissionService ?? throw new ArgumentNullException(nameof(dappPermissionService));
            _dappPermissionPromptService = dappPermissionPromptService ?? throw new ArgumentNullException(nameof(dappPermissionPromptService));
            _chainAdditionPromptService = chainAdditionPromptService ?? throw new ArgumentNullException(nameof(chainAdditionPromptService));
            _chainSwitchPromptService = chainSwitchPromptService ?? throw new ArgumentNullException(nameof(chainSwitchPromptService));
            _defaultChainId = defaultChainId;

            Console.WriteLine($"[WalletHostProvider] instance={_instanceId} promptService={_dappPermissionPromptService.GetType().FullName}");
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
            var web3 = new Web3.Web3(client);

            if (_interceptor == null)
            {
                InitializeInterceptor();
            }

            _interceptor!.SetSelectedAccount(_selectedAccount.Address);

            web3.Client.OverridingRequestInterceptor = _interceptor;
            return web3;
        }

        public async Task<IWeb3> GetWalletWeb3Async()
        {
            if (_selectedAccount == null)
                throw new InvalidOperationException("No account selected. Please select an account first.");

            if (_activeChain == null)
                throw new InvalidOperationException("No chain selected. Please select a network first.");

            var client = await _rpcClientFactory.CreateClientAsync(_activeChain);
            return await _selectedAccount.CreateWeb3Async(client);
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

        public async Task LogoutAsync()
        {
            await _vaultService.LockAsync();
            if (_selectedAccount != null)
            {
                _selectedAccount.IsSelected = false;
                _selectedAccount = null;
                if (SelectedAccountChanged != null)
                    await SelectedAccountChanged.Invoke(string.Empty);
            }
            _accounts.Clear();
            Enabled = false;
            await RaiseEnabledChangedAsync(false);
        }

        public Task<string> GetProviderSelectedAccountAsync() => Task.FromResult(SelectedAccount);

        public async Task<string> SignMessageAsync(string message)
        {
            if (_selectedAccount == null)
                throw new InvalidOperationException("No account selected.");
            var promptContext = CreatePersonalSignContext(message, _selectedAccount.Address);
            var approved = await RequestPersonalSignAsync(promptContext).ConfigureAwait(false);

            if (!approved)
            {
                throw new OperationCanceledException("User rejected personal_sign request.");
            }

            var web3 = await GetWalletWeb3Async().ConfigureAwait(false);
            var messageHex = message.IsHex() ? message : message.ToHexUTF8();
            return await web3.Eth.AccountSigning.PersonalSign.SendRequestAsync(message.HexToByteArray(), _selectedAccount.Address).ConfigureAwait(false);
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

        public Task<bool> RequestPersonalSignAsync(SignaturePromptContext context)
            => _signaturePromptService.PromptSignatureAsync(context);

        public Task<bool> RequestTypedDataSignAsync(TypedDataSignPromptContext context)
            => _signaturePromptService.PromptTypedDataSignAsync(context);

        public async Task<bool> RequestDappPermissionAsync(DappConnectionContext dappContext, string accountAddress)
        {
            if (dappContext == null || string.IsNullOrWhiteSpace(dappContext.Origin))
            {
                return true;
            }

            var normalizedAccount = accountAddress?.Trim().ToLowerInvariant() ?? string.Empty;
            var normalizedOrigin = dappContext.Origin.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(normalizedAccount))
            {
                return false;
            }

            if (await _dappPermissionService.IsApprovedAsync(normalizedOrigin, normalizedAccount).ConfigureAwait(false))
            {
                return true;
            }

            Console.WriteLine($"[WalletHostProvider] RequestDappPermissionAsync instance={_instanceId} origin={dappContext.Origin} account={accountAddress}");

            var approved = await _dappPermissionPromptService.RequestPermissionAsync(new DappPermissionPromptRequest
            {
                Origin = dappContext.Origin,
                DappName = dappContext.Title,
                DappIcon = dappContext.Icon,
                AccountAddress = accountAddress
            }).ConfigureAwait(false);

            Console.WriteLine($"[WalletHostProvider] Prompt result instance={_instanceId} approved={approved}");
            if (approved)
            {
                await _dappPermissionService.ApproveAsync(normalizedOrigin, normalizedAccount).ConfigureAwait(false);
                return true;
            }

            await _dappPermissionService.RevokeAsync(normalizedOrigin, normalizedAccount).ConfigureAwait(false);
            return false;
        }

        public Task<ChainAdditionPromptResult> RequestChainAdditionAsync(ChainAdditionPromptRequest request)
            => _chainAdditionPromptService.RequestAddChainAsync(request);

        public async Task<ChainSwitchPromptResult> RequestChainSwitchAsync(ChainSwitchPromptRequest request)
        {
            if (request == null)
            {
                return ChainSwitchPromptResult.Rejected("Invalid request");
            }

            var chainId = request.ChainId;
            if (chainId == default)
            {
                return ChainSwitchPromptResult.Rejected("Invalid chain identifier");
            }

            try
            {
                if (_activeChain != null)
                {
                    try
                    {
                        request.CurrentChainId = (long)_activeChain.ChainId;
                    }
                    catch
                    {
                        request.CurrentChainId = SelectedNetworkChainId;
                    }

                    request.CurrentChain = _activeChain;
                }
                else
                {
                    request.CurrentChainId = SelectedNetworkChainId;
                    request.CurrentChain = _configurationService.ActiveChain;
                }

                var knownChain = _configurationService.GetChain(chainId);
                request.IsKnown = knownChain != null;

                if (request.Chain == null)
                {
                    request.Chain = knownChain ?? await _chainManagementService.GetCompleteChainAsync(chainId).ConfigureAwait(false);
                }

                var approved = await _chainSwitchPromptService.RequestSwitchAsync(request).ConfigureAwait(false);
                if (!approved)
                {
                    return ChainSwitchPromptResult.Rejected();
                }

                var chainFeature = request.Chain;
                var chainAdded = false;

                if (!request.IsKnown && request.AllowAdd)
                {
                    if (chainFeature == null)
                    {
                        return ChainSwitchPromptResult.Failure("Network metadata unavailable to add.");
                    }

                    try
                    {
                        await _chainManagementService.AddCustomChainAsync(chainFeature).ConfigureAwait(false);
                        await _configurationService.AddOrUpdateChainAsync(chainFeature).ConfigureAwait(false);
                        chainAdded = true;
                    }
                    catch (Exception ex)
                    {
                        return ChainSwitchPromptResult.Failure($"Failed to add network: {ex.Message}");
                    }
                }

                var switched = await SetSelectedNetworkAsync((long)chainId).ConfigureAwait(false);
                if (!switched)
                {
                    return ChainSwitchPromptResult.Failure("Failed to switch network.", chainAdded, false);
                }

                return ChainSwitchPromptResult.Success(chainAdded);
            }
            catch (Exception ex)
            {
                return ChainSwitchPromptResult.Failure(ex.Message);
            }
        }

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
            else
            {
                var storedAccountAddress = await _storageService.GetSelectedAccountAsync();
                var candidate = string.IsNullOrEmpty(storedAccountAddress)
                    ? _accounts.First()
                    : _accounts.FirstOrDefault(a => a.Address.Equals(storedAccountAddress, StringComparison.OrdinalIgnoreCase)) ?? _accounts.First();
                await SetSelectedWalletAccountAsync(candidate);
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

        private SignaturePromptContext CreatePersonalSignContext(string message, string address)
        {
            var isHex = message.IsHex();
            string? decoded = null;

            if (isHex)
            {
                try
                {
                    var bytes = message.HexToByteArray();
                    decoded = System.Text.Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    decoded = null;
                }
            }

            return new SignaturePromptContext
            {
                Method = "personal_sign",
                Message = message,
                DecodedMessage = decoded,
                IsMessageHex = isHex,
                Address = address,
                Origin = SelectedDapp?.Origin,
                DappName = SelectedDapp?.Title,
                DappIcon = SelectedDapp?.Icon
            };
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
