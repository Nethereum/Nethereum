using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.SendTransaction.Models;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Diagnostics;
using Nethereum.Wallet.WalletAccounts;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public partial class SendNativeTokenViewModel : ObservableObject
    {
        private readonly IComponentLocalizer<SendNativeTokenViewModel> _localizer;
        private readonly IChainManagementService _chainManagementService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IPendingTransactionService? _pendingTransactionService;
        private readonly ITokenManagementService? _tokenManagementService;
        private readonly IErc20TokenService? _erc20TokenService;

        [ObservableProperty] private TokenNativeTransferModel _nativeTransfer = null!;
        [ObservableProperty] private TransactionViewModel _transaction = null!;

        [ObservableProperty] private ObservableCollection<ChainFeature> _availableChains = new();
        [ObservableProperty] private ChainFeature? _selectedChain;

        [ObservableProperty] private ObservableCollection<IWalletAccount> _availableAccounts = new();
        [ObservableProperty] private IWalletAccount? _selectedAccount;

        [ObservableProperty] private ObservableCollection<AccountToken> _availableTokens = new();
        [ObservableProperty] private ObservableCollection<AccountToken> _allKnownTokens = new();
        [ObservableProperty] private AccountToken? _selectedToken;
        [ObservableProperty] private bool _isLoadingTokens;
        [ObservableProperty] private bool _isRefreshingBalances;
        [ObservableProperty] private bool _isSearchingTokens;
        [ObservableProperty] private HashSet<string> _loadingBalanceTokens = new();

        [ObservableProperty] private decimal? _tokenPrice;
        [ObservableProperty] private string _currencySymbol = "$";
        
        [ObservableProperty] private int _currentStep = 1;
        [ObservableProperty] private int _totalSteps = 3;
        
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private string? _transactionHash;
        
        public SendNativeTokenViewModel(
            IComponentLocalizer<SendNativeTokenViewModel> localizer,
            TokenNativeTransferModel nativeTransferModel,
            TransactionViewModel transactionViewModel,
            IChainManagementService chainManagementService,
            NethereumWalletHostProvider walletHostProvider,
            IPendingTransactionService? pendingTransactionService = null,
            ITokenManagementService? tokenManagementService = null,
            IErc20TokenService? erc20TokenService = null)
        {
            _localizer = localizer;
            _nativeTransfer = nativeTransferModel;
            _transaction = transactionViewModel;
            _chainManagementService = chainManagementService;
            _walletHostProvider = walletHostProvider;
            _pendingTransactionService = pendingTransactionService;
            _tokenManagementService = tokenManagementService;
            _erc20TokenService = erc20TokenService;

            SetupModelSynchronization();
        }
        
        public async Task InitializeAsync()
        {
            try
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "InitializeAsync start");

                await LoadChainsAsync();
                await LoadAccountsAsync();

                if (SelectedAccount == null)
                {
                    ErrorMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.NoAccountSelected);
                    return;
                }

                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"InitializeAsync selected account {SelectedAccount.Address}");

                NativeTransfer.FromAddress = SelectedAccount.Address;
                Transaction.Transaction.FromAddress = SelectedAccount.Address;
                Transaction.Initialize(Transaction.Transaction);

                await RefreshKnownTokenBalancesAsync();

                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "InitializeAsync completed");
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"InitializeAsync error: {ex.Message}");
                ErrorMessage = $"Failed to initialize: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SelectTokenAsync(AccountToken token)
        {
            if (token == null) return;

            SelectedToken = token;
            NativeTransfer.SetToken(
                token.Symbol,
                token.Decimals,
                token.IsNative,
                token.ContractAddress,
                token.LogoURI,
                token.ChainId
            );
            NativeTransfer.AvailableBalance = token.Balance;
            NativeTransfer.ChainId = token.ChainId;

            OnPropertyChanged(nameof(CanProceedToNextStep));
            OnPropertyChanged(nameof(CanSendTransaction));
        }

        public void PreSelectToken(string symbol, string? contractAddress, long chainId, BigInteger balance, int decimals, string? logoUri)
        {
            var token = new AccountToken
            {
                Symbol = symbol,
                ContractAddress = contractAddress,
                ChainId = chainId,
                Balance = balance,
                Decimals = decimals,
                LogoURI = logoUri,
                IsNative = string.IsNullOrEmpty(contractAddress)
            };
            _ = SelectTokenAsync(token);
        }
        
        public void Initialize(string fromAddress, string tokenSymbol = "ETH", int tokenDecimals = 18)
        {
            NativeTransfer.FromAddress = fromAddress;
            NativeTransfer.TokenSymbol = tokenSymbol;
            NativeTransfer.TokenDecimals = tokenDecimals;
            
            Transaction.Transaction.FromAddress = fromAddress;
            Transaction.Initialize(Transaction.Transaction);
        }
        
        private void SetupModelSynchronization()
        {
            NativeTransfer.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TokenNativeTransferModel.RecipientAddress) ||
                    e.PropertyName == nameof(TokenNativeTransferModel.Amount) ||
                    e.PropertyName == nameof(TokenNativeTransferModel.IsNativeToken))
                {
                    SyncTransactionFromModel();
                }
                else if (e.PropertyName == nameof(TokenNativeTransferModel.Nonce))
                {
                    Transaction.Transaction.Nonce = NativeTransfer.Nonce;
                }

                OnPropertyChanged(nameof(CanProceedToNextStep));
                OnPropertyChanged(nameof(CanSendTransaction));
            };
        }

        private void SyncTransactionFromModel()
        {
            Transaction.Transaction.RecipientAddress = NativeTransfer.GetTransactionToAddress();

            if (NativeTransfer.IsNativeToken)
            {
                Transaction.Transaction.Amount = NativeTransfer.Amount;
                Transaction.Transaction.Data = NativeTransfer.TransactionData;
            }
            else
            {
                Transaction.Transaction.Amount = "0";
                Transaction.Transaction.Data = NativeTransfer.GetEncodedTransferData();
            }
        }
        
        public bool CanProceedToNextStep => CurrentStep switch
        {
            1 => IsStep1Valid,
            2 => IsStep2Valid,
            _ => false
        };
        
        public bool CanGoBack => CurrentStep > 1;
        
        public bool CanSendTransaction => 
            CurrentStep == 2 && 
            NativeTransfer.IsValid && 
            Transaction.HasValidTransaction();
        
        public bool IsStep1Valid => 
            !string.IsNullOrWhiteSpace(NativeTransfer.RecipientAddress) &&
            !string.IsNullOrWhiteSpace(NativeTransfer.Amount) &&
            NativeTransfer.ValidateAmountBalance() &&
            !NativeTransfer.HasFieldErrors(nameof(TokenNativeTransferModel.RecipientAddress)) &&
            !NativeTransfer.HasFieldErrors(nameof(TokenNativeTransferModel.Amount));
        
        public bool IsStep2Valid => 
            IsStep1Valid &&
            Transaction.Transaction.GasConfiguration?.IsValid == true &&
            !string.IsNullOrWhiteSpace(Transaction.Transaction.Nonce);
        
        [RelayCommand]
        private async Task NextStepAsync()
        {
            Console.WriteLine($"[DEBUG] NextStepAsync: CurrentStep={CurrentStep}, CanProceedToNextStep={CanProceedToNextStep}");
            
            if (!CanProceedToNextStep || CurrentStep >= TotalSteps) return;
            
            Transaction.ValidationError = null;
            
            if (CurrentStep == 1)
            {
                NativeTransfer.ValidateAll();
                if (!IsStep1Valid)
                {
                    ErrorMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.PleaseCorrectErrors);
                    return;
                }
            }
            
            CurrentStep++;
            Console.WriteLine($"[DEBUG] Moved to step {CurrentStep}");
            
            if (CurrentStep == 2)
            {
                Console.WriteLine("[DEBUG] Loading gas configuration and nonce...");
                await Transaction.EstimateGasLimitAsync();
                await Transaction.FetchGasPriceAsync();
                await Transaction.RefreshNonceCommand.ExecuteAsync(null);
                Console.WriteLine($"[DEBUG] Gas loaded. Nonce={Transaction.Transaction.Nonce}, GasLimit={Transaction.Transaction.GasConfiguration?.CustomGasLimit}");
            }
        }
        
        [RelayCommand]
        private void PreviousStep()
        {
            if (CanGoBack)
            {
                var previousStep = CurrentStep;
                CurrentStep--;
                ClearMessages();

                if (previousStep == 2)
                {
                    Transaction.ResetPreviewState();
                    Transaction.ResetGasState();
                }
            }
        }
        
        [RelayCommand]
        private void SetMaxAmount()
        {
            NativeTransfer.SetMaxAmount();
        }
        
        [RelayCommand]
        private async Task SendTransactionAsync()
        {
            WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"SendTransactionAsync invoked (CurrentStep={CurrentStep})");
            Console.WriteLine($"[DEBUG] SendTransactionAsync called. CurrentStep={CurrentStep}");
            Console.WriteLine($"[DEBUG] CanSendTransaction={CanSendTransaction}");
            
            if (!CanSendTransaction)
            {
                Console.WriteLine($"[DEBUG] Cannot send: CurrentStep={CurrentStep}, NativeTransfer.IsValid={NativeTransfer.IsValid}, Transaction.HasValidTransaction={Transaction.HasValidTransaction()}");
                return;
            }
            
            var validationError = Transaction.ValidateTransaction();
            Console.WriteLine($"[DEBUG] ValidateTransaction result: {validationError ?? "No errors"}");
            
            if (!string.IsNullOrEmpty(validationError))
            {
                Console.WriteLine($"[DEBUG] Validation failed: {validationError}");
                Transaction.ValidationError = validationError;
                ErrorMessage = validationError;
                return;
            }
            
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;
            
            try
            {
                Console.WriteLine("[DEBUG] Calling Transaction.SendTransactionAsync()...");
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "SendTransactionAsync calling Transaction.SendTransactionAsync");
                var txHash = await Transaction.SendTransactionAsync();
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"SendTransactionAsync result: {(txHash ?? "null")}");
                Console.WriteLine($"[DEBUG] Transaction result: {txHash ?? "null"}");
                
                if (!string.IsNullOrEmpty(txHash))
                {
                    TransactionHash = txHash;
                    SuccessMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.TransactionSent);
                    OnTransactionSent?.Invoke(txHash);
                    CurrentStep = 3;
                }
                else
                {
                    ErrorMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.TransactionFailed);
                    CurrentStep = 2;
                    return;
                }
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"SendTransactionAsync error: {ex.Message}");
                ErrorMessage = $"{_localizer.GetString(SendNativeTokenLocalizer.Keys.TransactionFailed)}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task SimulateTransactionAsync()
        {
            await Transaction.SimulateTransactionCommand.ExecuteAsync(null);
        }
        
        [RelayCommand]
        private void Reset()
        {
            CurrentStep = 1;
            NativeTransfer.Reset();
            ClearMessages();
        }
        
        private void ClearMessages()
        {
            ErrorMessage = null;
            SuccessMessage = null;
            TransactionHash = null;
        }
        
        public void ValidateCurrentStep()
        {
            switch (CurrentStep)
            {
                case 1:
                    NativeTransfer.ValidateAll();
                    break;
                case 2:
                    Transaction.Transaction.GasConfiguration?.ValidateAll();
                    break;
                case 3:
                    NativeTransfer.ValidateAll();
                    break;
            }
        }
        
        public string GetStepTitle() => CurrentStep switch
        {
            1 => _localizer.GetString(SendNativeTokenLocalizer.Keys.Step1Title),
            2 => _localizer.GetString(SendNativeTokenLocalizer.Keys.Step2Title),
            3 => _localizer.GetString(SendNativeTokenLocalizer.Keys.Step3Title),
            _ => ""
        };
        
        public async Task<string?> GetExplorerUrlAsync()
        {
            if (string.IsNullOrEmpty(TransactionHash)) return null;
            
            try
            {
                var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
                var chain = await _chainManagementService.GetChainAsync(chainId);
                
                if (chain?.Explorers?.Count > 0)
                {
                    var explorerUrl = chain.Explorers.First();
                    return $"{explorerUrl.TrimEnd('/')}/tx/{TransactionHash}";
                }
            }
            catch
            {
            }
            
            return null;
        }
        
        [RelayCommand]
        private async Task ViewTransactionAsync()
        {
            var explorerUrl = await GetExplorerUrlAsync();
            if (!string.IsNullOrEmpty(explorerUrl))
            {
                // Open in browser - implementation would depend on platform
            }
        }
        
        public Action<string>? OnTransactionSent { get; set; }
        public Action? OnExit { get; set; }

        public string FormattedBalanceValue
        {
            get
            {
                if (SelectedToken == null || !SelectedToken.Price.HasValue) return null;
                var balance = ConvertToDecimal(SelectedToken.Balance, SelectedToken.Decimals);
                var value = balance * SelectedToken.Price.Value;
                return $"{CurrencySymbol}{value:N2}";
            }
        }

        public string FormattedAmountValue
        {
            get
            {
                if (SelectedToken == null || !SelectedToken.Price.HasValue) return null;
                if (!decimal.TryParse(NativeTransfer?.Amount, out var amount)) return null;
                var value = amount * SelectedToken.Price.Value;
                return $"{CurrencySymbol}{value:N2}";
            }
        }

        public bool HasPrice => SelectedToken?.Price.HasValue == true;

        partial void OnSelectedChainChanged(ChainFeature? value)
        {
            if (value != null)
            {
                _ = OnChainChangedAsync(value);
            }
        }

        partial void OnSelectedAccountChanged(IWalletAccount? value)
        {
            if (value != null)
            {
                _ = OnAccountChangedAsync(value);
            }
        }

        private async Task OnChainChangedAsync(ChainFeature chain)
        {
            WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Chain changed to {chain.ChainName} ({chain.ChainId})");

            await _walletHostProvider.SetSelectedNetworkAsync((long)chain.ChainId);

            await RefreshKnownTokenBalancesAsync();
            await LoadAllChainTokensAsync();
        }

        private async Task OnAccountChangedAsync(IWalletAccount account)
        {
            WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Account changed to {account.Address}");

            await _walletHostProvider.SetSelectedAccountAsync(account);

            NativeTransfer.FromAddress = account.Address;
            Transaction.Transaction.FromAddress = account.Address;

            await RefreshKnownTokenBalancesAsync();
        }

        [RelayCommand]
        public async Task LoadChainsAsync()
        {
            try
            {
                var chains = await _chainManagementService.GetAllChainsAsync();
                AvailableChains.Clear();
                foreach (var chain in chains.OrderBy(c => c.IsTestnet).ThenBy(c => c.ChainName))
                {
                    AvailableChains.Add(chain);
                }

                var currentChainId = _walletHostProvider.SelectedNetworkChainId;
                SelectedChain = AvailableChains.FirstOrDefault(c => (long)c.ChainId == currentChainId)
                    ?? AvailableChains.FirstOrDefault();
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to load chains: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task LoadAccountsAsync()
        {
            try
            {
                var accounts = _walletHostProvider.GetAccounts();
                AvailableAccounts.Clear();
                foreach (var account in accounts)
                {
                    AvailableAccounts.Add(account);
                }

                var currentAccount = _walletHostProvider.GetSelectedAccount();
                SelectedAccount = currentAccount != null
                    ? AvailableAccounts.FirstOrDefault(a => a.Address.Equals(currentAccount.Address, StringComparison.OrdinalIgnoreCase))
                    : AvailableAccounts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to load accounts: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task RefreshKnownTokenBalancesAsync()
        {
            if (SelectedAccount == null || SelectedChain == null) return;

            IsRefreshingBalances = true;
            try
            {
                var chainId = (long)SelectedChain.ChainId;

                var web3 = await _walletHostProvider.GetWeb3Async();
                var nativeBalance = await web3.Eth.GetBalance.SendRequestAsync(SelectedAccount.Address);

                AvailableTokens.Clear();
                var nativeToken = new AccountToken
                {
                    Symbol = SelectedChain.NativeCurrency?.Symbol ?? "ETH",
                    Name = SelectedChain.NativeCurrency?.Name ?? "Ether",
                    Decimals = SelectedChain.NativeCurrency?.Decimals ?? 18,
                    Balance = nativeBalance.Value,
                    ChainId = chainId,
                    IsNative = true,
                    ContractAddress = null
                };
                AvailableTokens.Add(nativeToken);

                if (_tokenManagementService != null)
                {
                    var tokens = await _tokenManagementService.GetAccountTokensAsync(SelectedAccount.Address, chainId);
                    foreach (var token in tokens.Where(t => !t.IsHidden && !t.IsNative))
                    {
                        AvailableTokens.Add(token);
                    }
                }

                if (SelectedToken == null || SelectedToken.ChainId != chainId)
                {
                    SelectedToken = nativeToken;
                }
                else
                {
                    var existingToken = AvailableTokens.FirstOrDefault(t =>
                        string.Equals(t.ContractAddress, SelectedToken.ContractAddress, StringComparison.OrdinalIgnoreCase) &&
                        t.ChainId == SelectedToken.ChainId);
                    if (existingToken != null)
                    {
                        SelectedToken = existingToken;
                    }
                    else
                    {
                        SelectedToken = nativeToken;
                    }
                }

                OnPropertyChanged(nameof(FormattedBalanceValue));
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to refresh balances: {ex.Message}");
                ErrorMessage = $"Failed to refresh balances: {ex.Message}";
            }
            finally
            {
                IsRefreshingBalances = false;
            }
        }

        [RelayCommand]
        public async Task AddCustomTokenAsync(string contractAddress)
        {
            if (string.IsNullOrWhiteSpace(contractAddress) || SelectedChain == null) return;

            IsLoadingTokens = true;
            try
            {
                var chainId = (long)SelectedChain.ChainId;
                var added = await _tokenManagementService.AddCustomTokenAsync(chainId, contractAddress);

                if (added)
                {
                    await RefreshKnownTokenBalancesAsync();

                    var newToken = AvailableTokens.FirstOrDefault(t =>
                        string.Equals(t.ContractAddress, contractAddress, StringComparison.OrdinalIgnoreCase));
                    if (newToken != null)
                    {
                        await SelectTokenAsync(newToken);
                    }
                }
                else
                {
                    ErrorMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.TokenNotFound);
                }
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to add custom token: {ex.Message}");
                ErrorMessage = $"Failed to add token: {ex.Message}";
            }
            finally
            {
                IsLoadingTokens = false;
            }
        }

        public async Task ApplyPreSelectionAsync(string? accountAddress, long? chainId, string? tokenContract, string? tokenSymbol = null)
        {
            WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"ApplyPreSelectionAsync: account={accountAddress}, chain={chainId}, contract={tokenContract}, symbol={tokenSymbol}");

            if (chainId.HasValue)
            {
                var chain = AvailableChains.FirstOrDefault(c => (long)c.ChainId == chainId.Value);
                if (chain != null)
                {
                    SelectedChain = chain;
                    await Task.Delay(100);
                }
            }

            if (!string.IsNullOrEmpty(accountAddress))
            {
                var account = AvailableAccounts.FirstOrDefault(a =>
                    a.Address.Equals(accountAddress, StringComparison.OrdinalIgnoreCase));
                if (account != null)
                {
                    SelectedAccount = account;
                    await Task.Delay(100);
                }
            }

            if (!string.IsNullOrEmpty(tokenContract) || !string.IsNullOrEmpty(tokenSymbol))
            {
                await RefreshKnownTokenBalancesAsync();

                AccountToken? token = null;

                if (!string.IsNullOrEmpty(tokenContract))
                {
                    token = AvailableTokens.FirstOrDefault(t =>
                        string.Equals(t.ContractAddress, tokenContract, StringComparison.OrdinalIgnoreCase));
                }
                else if (!string.IsNullOrEmpty(tokenSymbol))
                {
                    token = AvailableTokens.FirstOrDefault(t =>
                        string.Equals(t.Symbol, tokenSymbol, StringComparison.OrdinalIgnoreCase));
                }

                if (token != null)
                {
                    await SelectTokenAsync(token);
                }
            }
        }

        private static decimal ConvertToDecimal(BigInteger balance, int decimals)
        {
            if (balance == 0) return 0;

            var divisor = BigInteger.Pow(10, decimals);
            var quotient = BigInteger.DivRem(balance, divisor, out var remainder);

            var decimalPart = decimals > 0
                ? (decimal)remainder / (decimal)Math.Pow(10, decimals)
                : 0;

            return (decimal)quotient + decimalPart;
        }

        [RelayCommand]
        public async Task SearchTokensAsync(string searchText)
        {
            if (SelectedChain == null || _erc20TokenService == null) return;
            if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < 2)
            {
                AllKnownTokens.Clear();
                return;
            }

            IsSearchingTokens = true;
            try
            {
                var chainId = (long)SelectedChain.ChainId;
                var tokenList = await _erc20TokenService.GetTokenListAsync(chainId);

                AllKnownTokens.Clear();
                var searchLower = searchText.ToLowerInvariant();

                foreach (var token in tokenList.Where(t =>
                    (t.Symbol?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                    (t.Name?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                    (t.Address?.ToLowerInvariant().Contains(searchLower) ?? false)))
                {
                    AllKnownTokens.Add(new AccountToken
                    {
                        Symbol = token.Symbol,
                        Name = token.Name,
                        ContractAddress = token.Address,
                        Decimals = token.Decimals,
                        LogoURI = token.LogoUri,
                        ChainId = chainId,
                        Balance = BigInteger.Zero
                    });
                }

                if (AllKnownTokens.Count > 0 && AllKnownTokens.Count < 100)
                {
                    await FetchBalancesForSearchResultsAsync();
                }
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to search tokens: {ex.Message}");
            }
            finally
            {
                IsSearchingTokens = false;
            }
        }

        private async Task FetchBalancesForSearchResultsAsync()
        {
            if (SelectedAccount == null || SelectedChain == null || _erc20TokenService == null) return;

            try
            {
                var web3 = await _walletHostProvider.GetWeb3Async();
                var chainId = (long)SelectedChain.ChainId;

                var tokenInfos = AllKnownTokens.Select(t => new TokenInfo
                {
                    Symbol = t.Symbol,
                    Name = t.Name,
                    Address = t.ContractAddress,
                    Decimals = t.Decimals,
                    LogoUri = t.LogoURI
                }).ToList();

                var balances = await _erc20TokenService.GetBalancesForTokensAsync(
                    web3,
                    SelectedAccount.Address,
                    tokenInfos);

                foreach (var balance in balances)
                {
                    var token = AllKnownTokens.FirstOrDefault(t =>
                        string.Equals(t.ContractAddress, balance.Token.Address, StringComparison.OrdinalIgnoreCase));
                    if (token != null)
                    {
                        token.Balance = balance.Balance;
                    }
                }

                OnPropertyChanged(nameof(AllKnownTokens));
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to fetch balances for search results: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task LoadAllChainTokensAsync()
        {
            if (SelectedChain == null || _erc20TokenService == null) return;

            IsLoadingTokens = true;
            try
            {
                var chainId = (long)SelectedChain.ChainId;
                var tokenList = await _erc20TokenService.GetTokenListAsync(chainId);

                AllKnownTokens.Clear();
                foreach (var token in tokenList.OrderBy(t => t.Symbol))
                {
                    AllKnownTokens.Add(new AccountToken
                    {
                        ContractAddress = token.Address,
                        Symbol = token.Symbol,
                        Name = token.Name,
                        Decimals = token.Decimals,
                        LogoURI = token.LogoUri,
                        ChainId = chainId,
                        Balance = BigInteger.Zero,
                        CoinGeckoId = token.CoinGeckoId
                    });
                }

                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Loaded {AllKnownTokens.Count} tokens for chain {chainId}");
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to load chain tokens: {ex.Message}");
            }
            finally
            {
                IsLoadingTokens = false;
            }
        }

        [RelayCommand]
        public async Task GetTokenBalanceAsync(AccountToken token)
        {
            if (token == null || SelectedAccount == null || SelectedChain == null || _erc20TokenService == null) return;
            if (string.IsNullOrEmpty(token.ContractAddress)) return;

            LoadingBalanceTokens.Add(token.ContractAddress);
            OnPropertyChanged(nameof(LoadingBalanceTokens));

            try
            {
                var web3 = await _walletHostProvider.GetWeb3Async();
                var tokenInfos = new List<TokenInfo>
                {
                    new TokenInfo
                    {
                        Symbol = token.Symbol,
                        Name = token.Name,
                        Address = token.ContractAddress,
                        Decimals = token.Decimals,
                        LogoUri = token.LogoURI
                    }
                };

                var balances = await _erc20TokenService.GetBalancesForTokensAsync(
                    web3,
                    SelectedAccount.Address,
                    tokenInfos);

                var balance = balances.FirstOrDefault();
                if (balance != null)
                {
                    token.Balance = balance.Balance;

                    if (balance.Balance > BigInteger.Zero)
                    {
                        var existingInAvailable = AvailableTokens.FirstOrDefault(t =>
                            t.ContractAddress?.Equals(token.ContractAddress, StringComparison.OrdinalIgnoreCase) == true);
                        if (existingInAvailable == null)
                        {
                            AvailableTokens.Add(token);
                        }
                        else
                        {
                            existingInAvailable.Balance = balance.Balance;
                        }
                    }

                    OnPropertyChanged(nameof(AllKnownTokens));
                    OnPropertyChanged(nameof(AvailableTokens));
                }
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"Failed to get balance for token {token.Symbol}: {ex.Message}");
            }
            finally
            {
                LoadingBalanceTokens.Remove(token.ContractAddress);
                OnPropertyChanged(nameof(LoadingBalanceTokens));
            }
        }
    }
}
