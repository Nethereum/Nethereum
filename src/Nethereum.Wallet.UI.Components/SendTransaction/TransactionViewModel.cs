using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transaction;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.SendTransaction.Models;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public partial class TransactionViewModel : ObservableObject
    {
        private readonly IComponentLocalizer<TransactionViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly ITransactionDataDecodingService _dataDecodingService;
        private readonly IGasPriceProvider _gasPriceProvider;
        private readonly IChainManagementService _chainManagementService;
        private readonly IGasConfigurationPersistenceService _gasPersistenceService;
        private readonly IPendingTransactionService _pendingTransactionService;
        private readonly Components.TransactionStatusViewModel _statusViewModel;
        
        private const string DEFAULT_TOKEN_SYMBOL = "ETH";
        
        [ObservableProperty] private TransactionModel _transaction = null!;
        [ObservableProperty] private Components.TransactionStatusViewModel? _transactionStatus;
        
        [ObservableProperty] private bool _isEstimatingGas = false;
        [ObservableProperty] private string? _gasEstimationError;
        [ObservableProperty] private bool _isLoadingNonce = false;
        [ObservableProperty] private string? _nonceError;
        
        [ObservableProperty] private TransactionDataInfo _decodedData = new();
        [ObservableProperty] private bool _showRawData = false;
        [ObservableProperty] private bool _isSimulating = false;
        [ObservableProperty] private string? _simulationResult;
        [ObservableProperty] private bool _isLoadingDecoding = false;
        
        [ObservableProperty] private string _tokenSymbol = "ETH";
        [ObservableProperty] private string _networkName = "";
        [ObservableProperty] private string _chainId = "";
        [ObservableProperty] private bool _isNonceEditable = true;
        [ObservableProperty] private bool _isGasEditable = true;
        
        [ObservableProperty] private decimal _gasBufferPercentage = 1.10m;
        
        [ObservableProperty] private bool _isLoadingGasStrategies = false;
        [ObservableProperty] private Dictionary<GasStrategy, GasStrategyDisplay> _gasStrategies = new();
        [ObservableProperty] private string? _gasStrategyHint;
        [ObservableProperty] private bool _canSwitchGasMode = false;
        
        [ObservableProperty] private string _baseGasDisplay = "";
        [ObservableProperty] private string _adjustedGasDisplay = "";
        [ObservableProperty] private string _multiplierDescription = "";
        [ObservableProperty] private string? _validationError;
        
        public TransactionViewModel(
            IComponentLocalizer<TransactionViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider,
            ITransactionDataDecodingService dataDecodingService,
            IGasPriceProvider gasPriceProvider,
            IChainManagementService chainManagementService,
            IGasConfigurationPersistenceService gasPersistenceService,
            IPendingTransactionService pendingTransactionService,
            Components.TransactionStatusViewModel statusViewModel)
        {
            _localizer = localizer;
            _walletHostProvider = walletHostProvider;
            _dataDecodingService = dataDecodingService;
            _gasPriceProvider = gasPriceProvider;
            _chainManagementService = chainManagementService;
            _gasPersistenceService = gasPersistenceService;
            _pendingTransactionService = pendingTransactionService;
            _statusViewModel = statusViewModel;
            
            _transaction = new TransactionModel(localizer);
            
            SetupPropertyListeners();
        }
        
        public void Initialize(TransactionModel transaction)
        {
            Transaction = transaction;
            SetupPropertyListeners();
        }
        
        public async Task InitializeFromTransactionInput(TransactionInput transactionInput)
        {
            if (transactionInput == null)
                throw new ArgumentNullException(nameof(transactionInput));
            
            var model = new TransactionModel(_localizer);
            
            model.FromAddress = transactionInput.From ?? _walletHostProvider.SelectedAccount ?? "";
            model.RecipientAddress = transactionInput.To ?? "";
            model.Amount = transactionInput.Value?.Value.ToString() ?? "0";
            model.Data = transactionInput.Data ?? "";
            model.Nonce = transactionInput.Nonce?.Value.ToString() ?? "";
            
            if (transactionInput.Gas != null)
            {
                model.GasConfiguration.CustomGasLimit = transactionInput.Gas.Value.ToString();
            }
            
            if (transactionInput.GasPrice != null)
            {
                model.GasConfiguration.IsEip1559Enabled = false;
                model.GasConfiguration.CustomGasPrice = transactionInput.GasPrice.Value.ToString();
            }
            
            if (transactionInput.MaxFeePerGas != null && transactionInput.MaxPriorityFeePerGas != null)
            {
                model.GasConfiguration.IsEip1559Enabled = true;
                model.GasConfiguration.CustomMaxFee = transactionInput.MaxFeePerGas.Value.ToString();
                model.GasConfiguration.CustomPriorityFee = transactionInput.MaxPriorityFeePerGas.Value.ToString();
            }
            
            Initialize(model);
            
            var chainInfo = await _chainManagementService.GetChainAsync(new BigInteger(_walletHostProvider.SelectedNetworkChainId));
            if (chainInfo != null)
            {
                TokenSymbol = chainInfo.NativeCurrency?.Symbol ?? DEFAULT_TOKEN_SYMBOL;
                NetworkName = chainInfo.ChainName ?? "";
                ChainId = chainInfo.ChainId.ToString();
            }
            
            if (string.IsNullOrWhiteSpace(model.Nonce))
            {
                await RefreshNonceAsync();
            }
            
            if (string.IsNullOrWhiteSpace(model.GasConfiguration.CustomGasLimit))
            {
                await EstimateGasLimitAsync();
            }
            
            if (!model.GasConfiguration.HasCustomPricing())
            {
                await FetchGasPriceAsync();
            }
            
            if (!string.IsNullOrWhiteSpace(model.Data))
            {
                await DecodeTransactionDataAsync();
            }
        }
        
        private void SetupPropertyListeners()
        {
            Transaction.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(TransactionModel.RecipientAddress) ||
                    e.PropertyName == nameof(TransactionModel.Amount) ||
                    e.PropertyName == nameof(TransactionModel.Data))
                {
                    if (CanEstimateGas())
                    {
                        await EstimateGasLimitAsync();
                        await FetchGasPriceAsync();
                    }
                }
                
                if (e.PropertyName == nameof(TransactionModel.Data))
                {
                    await DecodeTransactionDataAsync();
                }
            };
        }
        
        public bool CanEstimateGas()
        {
            return !string.IsNullOrWhiteSpace(Transaction.RecipientAddress) ||
                   !string.IsNullOrWhiteSpace(Transaction.Data);
        }
        
        public bool HasValidTransaction()
        {
            return Transaction.IsValid && 
                   Transaction.GasConfiguration?.IsValid == true &&
                   !string.IsNullOrWhiteSpace(Transaction.Nonce);
        }
        
        public string? ValidateTransaction()
        {
            ValidationError = null;
            
            if (string.IsNullOrWhiteSpace(Transaction.RecipientAddress))
            {
                ValidationError = _localizer.GetString(TransactionLocalizer.Keys.RecipientRequired);
                return ValidationError;
            }
            
            if (string.IsNullOrWhiteSpace(Transaction.Amount) || !decimal.TryParse(Transaction.Amount, out var amt) || amt <= 0)
            {
                ValidationError = _localizer.GetString(TransactionLocalizer.Keys.InvalidAmount);
                return ValidationError;
            }
            
            if (Transaction.GasConfiguration?.IsValid != true)
            {
                ValidationError = _localizer.GetString(TransactionLocalizer.Keys.InvalidGasConfiguration);
                return ValidationError;
            }
            
            if (string.IsNullOrWhiteSpace(Transaction.Nonce))
            {
                ValidationError = _localizer.GetString(TransactionLocalizer.Keys.NonceRequired);
                return ValidationError;
            }
            
            return null;
        }
        
        [RelayCommand]
        public async Task DecodeTransactionDataAsync()
        {
            if (string.IsNullOrWhiteSpace(Transaction.Data))
            {
                DecodedData = new TransactionDataInfo();
                return;
            }

            try
            {
                DecodedData = await _dataDecodingService.DecodeTransactionDataAsync(
                    Transaction.Data, 
                    Transaction.RecipientAddress);
            }
            catch (Exception ex)
            {
                DecodedData = new TransactionDataInfo
                {
                    RawData = Transaction.Data,
                    DecodingError = ex.Message
                };
            }
        }

        [RelayCommand]
        public async Task EstimateGasLimitAsync()
        {
            if (!CanEstimateGas()) return;
            
            IsEstimatingGas = true;
            GasEstimationError = null;
            
            try
            {
                var web3 = await _walletHostProvider.GetWeb3Async();
                var transactionInput = Transaction.BuildTransactionInput();
                
                if (transactionInput != null)
                {
                    var gasEstimate = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(transactionInput);
                    var gasLimitWithBuffer = BigInteger.Multiply(gasEstimate.Value, new BigInteger((double)GasBufferPercentage * 100)) / 100;
                    
                    Transaction.GasConfiguration.CustomGasLimit = gasLimitWithBuffer.ToString();
                }
            }
            catch (Exception ex)
            {
                GasEstimationError = $"{_localizer.GetString(TransactionLocalizer.Keys.GasEstimationFailed)}: {ex.Message}";
                Transaction.GasConfiguration.CustomGasLimit = TransactionConstants.DEFAULT_TRANSFER_GAS_LIMIT.ToString();
            }
            finally
            {
                IsEstimatingGas = false;
            }
        }
        
        [RelayCommand]
        public async Task FetchGasPriceAsync()
        {
            try
            {
                GasPriceSuggestion suggestion;
                
                if (Transaction.GasConfiguration.IsEip1559Enabled)
                {
                    suggestion = await _gasPriceProvider.GetEIP1559GasPriceAsync();
                    
                    if (!suggestion.MaxFeePerGas.HasValue || !suggestion.MaxPriorityFeePerGas.HasValue)
                    {
                        GasEstimationError = _localizer.GetString(TransactionLocalizer.Keys.EIP1559NotAvailable);
                        return;
                    }
                }
                else
                {
                    suggestion = await _gasPriceProvider.GetLegacyGasPriceAsync();
                }
                
                Transaction.GasConfiguration.LoadBaseValues(suggestion);
                UpdateGasDisplays();
            }
            catch (Exception ex)
            {
                GasEstimationError = $"{_localizer.GetString(TransactionLocalizer.Keys.GasPriceFetchFailed)}: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private async Task RefreshNonceAsync()
        {
            if (string.IsNullOrWhiteSpace(Transaction.FromAddress)) return;
            
            IsLoadingNonce = true;
            NonceError = null;
            
            try
            {
                var web3 = await _walletHostProvider.GetWeb3Async();
                var nonce = await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(Transaction.FromAddress);
                Transaction.Nonce = nonce.Value.ToString();
            }
            catch (Exception ex)
            {
                NonceError = $"{_localizer.GetString(TransactionLocalizer.Keys.NonceFetchFailed)}: {ex.Message}";
            }
            finally
            {
                IsLoadingNonce = false;
            }
        }
        
        [RelayCommand]
        private void ToggleRawDataDisplay()
        {
            ShowRawData = !ShowRawData;
        }

        [RelayCommand]
        private async Task SimulateTransactionAsync()
        {
            if (!Transaction.IsValid) return;
            
            IsSimulating = true;
            SimulationResult = null;
            
            try
            {
                var transactionInput = Transaction.BuildTransactionInput();
                if (transactionInput != null)
                {
                    var web3 = await _walletHostProvider.GetWeb3Async();
                    var result = await web3.Eth.Transactions.Call.SendRequestAsync(transactionInput);
                    SimulationResult = result;
                }
            }
            catch (Exception ex)
            {
                SimulationResult = $"{_localizer.GetString(TransactionLocalizer.Keys.SimulationFailed)}: {ex.Message}";
            }
            finally
            {
                IsSimulating = false;
            }
        }
        
        public async Task<TransactionInput?> PrepareTransactionAsync(int tokenDecimals = 18)
        {
            if (string.IsNullOrWhiteSpace(Transaction.Nonce))
            {
                await RefreshNonceAsync();
            }
            
            if (string.IsNullOrWhiteSpace(Transaction.GasConfiguration?.CustomGasLimit))
            {
                await EstimateGasLimitAsync();
            }
            
            return Transaction.BuildTransactionInput(tokenDecimals);
        }
        
        public async Task<string?> SendTransactionAsync()
        {
            if (!HasValidTransaction()) return null;
            
            try
            {
                var transactionInput = Transaction.BuildTransactionInput();
                if (transactionInput == null) return null;
                
                var web3 = await _walletHostProvider.GetWeb3Async();
                var txHash = await web3.TransactionManager.SendTransactionAsync(transactionInput);
                
                TransactionStatus = _statusViewModel;
                await TransactionStatus.InitializeAsync(
                    txHash,
                    Transaction.FromAddress,
                    Transaction.RecipientAddress,
                    Transaction.Amount,
                    startMonitoring: true
                );
                
                var transactionInfo = new TransactionInfo
                {
                    Hash = txHash,
                    From = Transaction.FromAddress,
                    To = Transaction.RecipientAddress,
                    Value = Transaction.Amount,
                    Type = string.IsNullOrWhiteSpace(Transaction.Data) ? TransactionType.NativeToken : TransactionType.GeneralTransaction,
                    ChainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId),
                    DisplayName = string.IsNullOrWhiteSpace(Transaction.Data) ? "ETH Transfer" : "Contract Interaction",
                    Status = Nethereum.Wallet.Services.Transactions.TransactionStatus.Pending,
                    SubmittedAt = DateTime.UtcNow,
                    Data = Transaction.Data,
                    Nonce = Transaction.Nonce
                };
                
                await _pendingTransactionService.SubmitTransactionAsync(transactionInfo);
                
                return txHash;
            }
            catch
            {
                throw;
            }
        }

        public string FormatCurrency(decimal value)
        {
            if (value >= 1)
                return value.ToString("F4").TrimEnd('0').TrimEnd('.');
            else if (value >= 0.001m)
                return value.ToString("F6").TrimEnd('0').TrimEnd('.');
            else
                return value.ToString("F8").TrimEnd('0').TrimEnd('.');
        }
        
        public string GetTotalGasCost()
        {
            var totalCostWei = Transaction.GasConfiguration?.CalculateTotalCost() ?? BigInteger.Zero;
            var gasCostEther = Nethereum.Util.UnitConversion.Convert.FromWei(
                totalCostWei, 
                Nethereum.Util.UnitConversion.EthUnit.Ether);
            return FormatCurrency(gasCostEther);
        }
        
        public string GetTotalTransactionCost(int tokenDecimals = 18)
        {
            if (!decimal.TryParse(Transaction.Amount, out var amountValue)) 
                return GetTotalGasCost();
                
            var gasCostWei = Transaction.GasConfiguration?.CalculateTotalCost() ?? BigInteger.Zero;
            var gasCostEther = Nethereum.Util.UnitConversion.Convert.FromWei(gasCostWei, Nethereum.Util.UnitConversion.EthUnit.Ether);
            var total = amountValue + gasCostEther;
            return FormatCurrency(total);
        }
        
        [RelayCommand]
        private async Task SelectMultiplierAsync(decimal multiplier)
        {
            Transaction.GasConfiguration.ApplyMultiplier(multiplier);
            UpdateGasDisplays();
            UpdateMultiplierDescription(multiplier);
        }
        
        [RelayCommand]
        private async Task EnableCustomModeAsync()
        {
            Transaction.GasConfiguration.EnableCustomMode();
            
            var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
            var saved = await _gasPersistenceService.GetCustomGasConfigurationAsync(chainId);
            if (saved != null)
            {
                Transaction.GasConfiguration.CustomGasLimit = saved.GasLimit ?? Transaction.GasConfiguration.CustomGasLimit;
                Transaction.GasConfiguration.CustomGasPrice = saved.GasPrice ?? "";
                Transaction.GasConfiguration.CustomMaxFee = saved.MaxFee ?? "";
                Transaction.GasConfiguration.CustomPriorityFee = saved.PriorityFee ?? "";
            }
        }
        
        [RelayCommand]
        private async Task SaveCustomGasAsync()
        {
            if (!Transaction.GasConfiguration.IsCustomMode) return;
            
            var config = new CustomGasConfiguration
            {
                GasLimit = Transaction.GasConfiguration.CustomGasLimit,
                GasPrice = Transaction.GasConfiguration.CustomGasPrice,
                MaxFee = Transaction.GasConfiguration.CustomMaxFee,
                PriorityFee = Transaction.GasConfiguration.CustomPriorityFee,
                IsEip1559 = Transaction.GasConfiguration.IsEip1559Enabled
            };
            
            var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
            await _gasPersistenceService.SaveCustomGasConfigurationAsync(chainId, config);
        }
        
        [RelayCommand]
        private async Task ToggleGasModeAsync(bool useEip1559)
        {
            if (Transaction.GasConfiguration.IsEip1559Enabled == useEip1559) return;
            
            Transaction.GasConfiguration.IsEip1559Enabled = useEip1559;
            
            // IMPORTANT: Clear values from the previous mode
            Transaction.GasConfiguration.ClearModeSpecificValues();
            
            var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
            await _gasPersistenceService.SaveGasModePreferenceAsync(chainId, useEip1559);
            
            await FetchGasPriceAsync();
        }
        
        private List<GasPriceSuggestion> FilterValidSuggestions(IList<GasPriceSuggestion> suggestions)
        {
            if (suggestions == null) return new List<GasPriceSuggestion>();
            
            var valid = new List<GasPriceSuggestion>();
            const int MAX_SUGGESTIONS = 3;
            
            foreach (var suggestion in suggestions)
            {
                if (IsValidSuggestion(suggestion))
                {
                    valid.Add(suggestion);
                    if (valid.Count >= MAX_SUGGESTIONS)
                        break;
                }
            }
            
            return valid;
        }
        
        private bool IsValidSuggestion(GasPriceSuggestion suggestion)
        {
            if (suggestion == null) return false;
            
            if (suggestion.MaxFeePerGas.HasValue && suggestion.MaxPriorityFeePerGas.HasValue)
            {
                return suggestion.MaxFeePerGas.Value > 0 && suggestion.MaxPriorityFeePerGas.Value >= 0;
            }
            
            if (suggestion.GasPrice.HasValue)
            {
                return suggestion.GasPrice.Value > 0;
            }
            
            return false;
        }
        
        private int GetStrategyIndex(GasStrategy strategy, int suggestionCount)
        {
            return strategy switch
            {
                GasStrategy.Fast => 0,
                GasStrategy.Normal => suggestionCount > 2 ? 1 : 0,
                GasStrategy.Slow => suggestionCount - 1,
                _ => Math.Min(1, suggestionCount - 1)
            };
        }
        
        private void UpdateGasStrategyDisplay(GasStrategy strategy, GasPriceSuggestion suggestion, int totalSuggestions)
        {
            if (totalSuggestions > 1)
            {
                GasStrategyHint = $"{_localizer.GetString(TransactionLocalizer.Keys.UsingStrategy)} {GetStrategyIndex(strategy, totalSuggestions) + 1}/{totalSuggestions}";
            }
            else
            {
                GasStrategyHint = _localizer.GetString(TransactionLocalizer.Keys.NetworkBasePriceUseCustom);
            }
        }
        
        private void UpdateGasDisplays()
        {
            if (Transaction.GasConfiguration.IsEip1559Enabled)
            {
                BaseGasDisplay = $"{Transaction.GasConfiguration.BaseMaxFee} / {Transaction.GasConfiguration.BasePriorityFee} Gwei";
                if (!Transaction.GasConfiguration.IsCustomMode)
                {
                    AdjustedGasDisplay = $"{Transaction.GasConfiguration.AdjustedMaxFee} / {Transaction.GasConfiguration.AdjustedPriorityFee} Gwei ({Transaction.GasConfiguration.SelectedMultiplier}x)";
                }
            }
            else
            {
                BaseGasDisplay = $"{Transaction.GasConfiguration.BaseGasPrice} Gwei";
                if (!Transaction.GasConfiguration.IsCustomMode)
                {
                    AdjustedGasDisplay = $"{Transaction.GasConfiguration.AdjustedGasPrice} Gwei ({Transaction.GasConfiguration.SelectedMultiplier}x)";
                }
            }
        }

        private void UpdateMultiplierDescription(decimal multiplier)
        {
            MultiplierDescription = multiplier switch
            {
                0.8m => _localizer.GetString(TransactionLocalizer.Keys.Multiplier08Description),
                1.0m => _localizer.GetString(TransactionLocalizer.Keys.Multiplier10Description),
                1.2m => _localizer.GetString(TransactionLocalizer.Keys.Multiplier12Description),
                _ => ""
            };
        }

        [RelayCommand]
        private async Task SwitchGasModeAsync(bool useEip1559)
        {
            if (Transaction.GasConfiguration.IsEip1559Enabled == useEip1559)
                return;
                
            Transaction.GasConfiguration.IsEip1559Enabled = useEip1559;
            
            await RefreshGasStrategyDisplayAsync();
        }
        
        private async Task RefreshGasStrategyDisplayAsync()
        {
            IsLoadingGasStrategies = true;
            GasStrategies.Clear();
            
            try
            {
                var suggestions = await _gasPriceProvider.GetGasPriceLevelsAsync();
                var validSuggestions = FilterValidSuggestions(suggestions);
                
                if (validSuggestions.Count > 0)
                {
                    foreach (GasStrategy strategy in Enum.GetValues<GasStrategy>())
                    {
                        if (strategy == GasStrategy.Custom) continue;
                        
                        int index = GetStrategyIndex(strategy, validSuggestions.Count);
                        var suggestion = validSuggestions[index];
                        
                        var display = CreateGasStrategyDisplay(strategy, suggestion);
                        GasStrategies[strategy] = display;
                    }
                }
            }
            catch (Exception ex)
            {
                GasEstimationError = $"{_localizer.GetString(TransactionLocalizer.Keys.RefreshGasStrategiesFailed)}: {ex.Message}";
            }
            finally
            {
                IsLoadingGasStrategies = false;
            }
        }
        
        private GasStrategyDisplay CreateGasStrategyDisplay(GasStrategy strategy, GasPriceSuggestion suggestion)
        {
            var totalCost = BigInteger.Zero;
            var gasLimit = Transaction.GasConfiguration.GasLimitValue;
            
            if (Transaction.GasConfiguration.IsEip1559Enabled && suggestion.MaxFeePerGas.HasValue)
            {
                totalCost = gasLimit * suggestion.MaxFeePerGas.Value;
            }
            else if (suggestion.GasPrice.HasValue)
            {
                totalCost = gasLimit * suggestion.GasPrice.Value;
            }
            
            var costInEther = Nethereum.Util.UnitConversion.Convert.FromWei(totalCost, Nethereum.Util.UnitConversion.EthUnit.Ether);
            
            return new GasStrategyDisplay
            {
                Strategy = strategy,
              EstimatedCost = FormatCurrency(costInEther),
                MaxFee = suggestion.MaxFeePerGas,
                PriorityFee = suggestion.MaxPriorityFeePerGas,
                GasPrice = suggestion.GasPrice,
                IsAvailable = true
            };
        }

        public async Task InitializeAsync()
        {
            var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
            
            try
            {
                ChainId = chainId.ToString();
                try
                {
                    var chainInfo = await _chainManagementService.GetChainAsync(chainId).ConfigureAwait(false);
                    if (chainInfo != null)
                    {
                        NetworkName = !string.IsNullOrEmpty(chainInfo.ChainName) ? chainInfo.ChainName : ChainId;
                        TokenSymbol = chainInfo.NativeCurrency?.Symbol ?? chainInfo.NativeCurrency?.Name ?? DEFAULT_TOKEN_SYMBOL;
                    }
                    else
                    {
                        NetworkName = ChainId;
                        TokenSymbol = DEFAULT_TOKEN_SYMBOL;
                    }
                }
                catch (Exception ex)
                {
                    NetworkName = ChainId;
                    TokenSymbol = DEFAULT_TOKEN_SYMBOL;
                    GasEstimationError = _localizer.GetString(TransactionLocalizer.Keys.NetworkInitializationWarning);
                }
                
                var supportsEIP1559 = await _gasPriceProvider.GetSupportsEIP1559Async().ConfigureAwait(false);
                var preferEip1559 = await _gasPersistenceService.GetGasModePreferenceAsync(chainId).ConfigureAwait(false);
                
                Transaction.GasConfiguration.CanToggleGasMode = supportsEIP1559;
                Transaction.GasConfiguration.IsEip1559Enabled = supportsEIP1559 && preferEip1559;
                CanSwitchGasMode = supportsEIP1559;
                
                await RefreshGasStrategyDisplayAsync().ConfigureAwait(false);
                
                if (CanEstimateGas())
                {
                    await EstimateGasLimitAsync().ConfigureAwait(false);
                }
                
                if (string.IsNullOrWhiteSpace(Transaction.Nonce))
                {
                    await RefreshNonceAsync().ConfigureAwait(false);
                }
                
                if (!string.IsNullOrWhiteSpace(Transaction.Data))
                {
                    await DecodeTransactionDataAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                GasEstimationError = $"{_localizer.GetString(TransactionLocalizer.Keys.InitializationFailed)}: {ex.Message}";
            }
        }
        
        public EventCallback<GasStrategy> OnGasStrategyChanged { get; set; }
        
        public TransactionDataInfo TransactionDataInfo => DecodedData;
        public string TotalGasCost => GetTotalGasCost();
        public string TotalTransactionCost => GetTotalTransactionCost();
    }
}