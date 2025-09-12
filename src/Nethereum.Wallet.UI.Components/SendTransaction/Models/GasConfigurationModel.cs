using System;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Util;
using Nethereum.Wallet.Services.Transaction;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Validation;
using Nethereum.Wallet.UI.Components.SendTransaction.Components;

namespace Nethereum.Wallet.UI.Components.SendTransaction.Models
{
    public partial class GasConfigurationModel : LocalizedValidationModel
    {
        public GasConfigurationModel(IComponentLocalizer localizer) : base(localizer) 
        {
        }
        
        [ObservableProperty] private GasStrategy _strategy = GasStrategy.Normal;
        [ObservableProperty] private string _customGasLimit = TransactionConstants.DEFAULT_TRANSFER_GAS_LIMIT.ToString();
        [ObservableProperty] private string _customGasPrice = "";
        [ObservableProperty] private string _customMaxFee = "";
        [ObservableProperty] private string _customPriorityFee = "";
        
        [ObservableProperty] private bool _isEip1559Enabled = false;
        
        [ObservableProperty] private decimal _selectedMultiplier = 1.0m;
        [ObservableProperty] private bool _isCustomMode = false;
        [ObservableProperty] private bool _canToggleGasMode = false;
        
        [ObservableProperty] private string _baseGasPrice = "";
        [ObservableProperty] private string _baseMaxFee = "";
        [ObservableProperty] private string _basePriorityFee = "";
        
        [ObservableProperty] private string _adjustedGasPrice = "";
        [ObservableProperty] private string _adjustedMaxFee = "";
        [ObservableProperty] private string _adjustedPriorityFee = "";
        
        public bool IsValid => !HasErrors;
        
        public BigInteger GasLimitValue => 
            BigInteger.TryParse(CustomGasLimit, out var limit) ? limit : TransactionConstants.DEFAULT_TRANSFER_GAS_LIMIT;
            
        public BigInteger GasPriceValue
        {
            get
            {
                if (IsCustomMode)
                {
                    return decimal.TryParse(CustomGasPrice, out var price)
                        ? UnitConversion.Convert.ToWei(price, UnitConversion.EthUnit.Gwei)
                        : BigInteger.Zero;
                }
                else
                {
                    return decimal.TryParse(AdjustedGasPrice, out var price)
                        ? UnitConversion.Convert.ToWei(price, UnitConversion.EthUnit.Gwei)
                        : BigInteger.Zero;
                }
            }
        }
        
        public BigInteger MaxFeeValue
        {
            get
            {
                if (IsCustomMode)
                {
                    return decimal.TryParse(CustomMaxFee, out var fee)
                        ? UnitConversion.Convert.ToWei(fee, UnitConversion.EthUnit.Gwei)
                        : BigInteger.Zero;
                }
                else
                {
                    return decimal.TryParse(AdjustedMaxFee, out var fee)
                        ? UnitConversion.Convert.ToWei(fee, UnitConversion.EthUnit.Gwei)
                        : BigInteger.Zero;
                }
            }
        }
        
        public BigInteger PriorityFeeValue
        {
            get
            {
                if (IsCustomMode)
                {
                    return decimal.TryParse(CustomPriorityFee, out var fee)
                        ? UnitConversion.Convert.ToWei(fee, UnitConversion.EthUnit.Gwei)
                        : BigInteger.Zero;
                }
                else
                {
                    return decimal.TryParse(AdjustedPriorityFee, out var fee)
                        ? UnitConversion.Convert.ToWei(fee, UnitConversion.EthUnit.Gwei)
                        : BigInteger.Zero;
                }
            }
        }
        
        partial void OnStrategyChanged(GasStrategy value)
        {
            OnPropertyChanged(nameof(IsValid));
        }
        
        partial void OnCustomGasLimitChanged(string value)
        {
            ValidateGasLimit();
            OnPropertyChanged(nameof(IsValid));
        }
        
        partial void OnCustomGasPriceChanged(string value)
        {
            ValidateGasPrice();
            OnPropertyChanged(nameof(IsValid));
        }
        
        partial void OnCustomMaxFeeChanged(string value)
        {
            ValidateMaxFee();
            OnPropertyChanged(nameof(IsValid));
        }
        
        partial void OnCustomPriorityFeeChanged(string value)
        {
            ValidatePriorityFee();
            OnPropertyChanged(nameof(IsValid));
        }
        
        private void ValidateGasLimit()
        {
            if (Strategy != GasStrategy.Custom)
            {
                SetFieldError(nameof(CustomGasLimit), null);
                return;
            }
            
            ValidateField(nameof(CustomGasLimit),
                (string.IsNullOrWhiteSpace(CustomGasLimit), 
                    SharedValidationLocalizer.Keys.FieldRequired),
                (!BigInteger.TryParse(CustomGasLimit, out var limit) || limit < TransactionConstants.DEFAULT_TRANSFER_GAS_LIMIT, 
                    SharedValidationLocalizer.Keys.GasLimitTooLow));
        }
        
        private void ValidateGasPrice()
        {
            if (Strategy != GasStrategy.Custom || IsEip1559Enabled)
            {
                SetFieldError(nameof(CustomGasPrice), null);
                return;
            }
            
            ValidateField(nameof(CustomGasPrice),
                (string.IsNullOrWhiteSpace(CustomGasPrice), 
                    SharedValidationLocalizer.Keys.FieldRequired),
                (!decimal.TryParse(CustomGasPrice, out var price) || price <= 0, 
                    SharedValidationLocalizer.Keys.AmountMustBePositive));
        }
        
        private void ValidateMaxFee()
        {
            if (Strategy != GasStrategy.Custom || !IsEip1559Enabled)
            {
                SetFieldError(nameof(CustomMaxFee), null);
                return;
            }
            
            ValidateField(nameof(CustomMaxFee),
                (string.IsNullOrWhiteSpace(CustomMaxFee), 
                    SharedValidationLocalizer.Keys.FieldRequired),
                (!decimal.TryParse(CustomMaxFee, out var fee) || fee <= 0, 
                    SharedValidationLocalizer.Keys.AmountMustBePositive),
                (PriorityFeeValue > MaxFeeValue && MaxFeeValue > 0, 
                    SharedValidationLocalizer.Keys.ValueOutOfRange));
        }
        
        private void ValidatePriorityFee()
        {
            if (Strategy != GasStrategy.Custom || !IsEip1559Enabled)
            {
                SetFieldError(nameof(CustomPriorityFee), null);
                return;
            }
            
            ValidateField(nameof(CustomPriorityFee),
                (string.IsNullOrWhiteSpace(CustomPriorityFee), 
                    SharedValidationLocalizer.Keys.FieldRequired),
                (!decimal.TryParse(CustomPriorityFee, out var fee) || fee < 0, 
                    SharedValidationLocalizer.Keys.ValueMustBeNonNegative));
                    
            ValidateMaxFee();
        }

        public BigInteger CalculateTotalCost()
        {
            if (IsCustomMode)
            {
                if (IsEip1559Enabled)
                    return GasLimitValue * MaxFeeValue;
                else
                    return GasLimitValue * GasPriceValue;
            }
            else
            {
                if (IsEip1559Enabled)
                {
                    if (decimal.TryParse(AdjustedMaxFee, out var fee))
                    {
                        var weiValue = UnitConversion.Convert.ToWei(fee, UnitConversion.EthUnit.Gwei);
                        return GasLimitValue * weiValue;
                    }
                }
                else
                {
                    if (decimal.TryParse(AdjustedGasPrice, out var price))
                    {
                        var weiValue = UnitConversion.Convert.ToWei(price, UnitConversion.EthUnit.Gwei);
                        return GasLimitValue * weiValue;
                    }
                }
            }
            return BigInteger.Zero;
        }
        
        public bool HasCustomPricing()
        {
            if (IsEip1559Enabled)
            {
                return !string.IsNullOrWhiteSpace(CustomMaxFee) && 
                       !string.IsNullOrWhiteSpace(CustomPriorityFee);
            }
            else
            {
                return !string.IsNullOrWhiteSpace(CustomGasPrice);
            }
        }
        
        public void LoadFromGasPriceSuggestion(GasPriceSuggestion suggestion)
        {
            if (suggestion.MaxFeePerGas.HasValue && suggestion.MaxPriorityFeePerGas.HasValue)
            {
                IsEip1559Enabled = true;
                CustomMaxFee = UnitConversion.Convert
                    .FromWei(suggestion.MaxFeePerGas.Value, UnitConversion.EthUnit.Gwei)
                    .ToString("F2");
                CustomPriorityFee = UnitConversion.Convert
                    .FromWei(suggestion.MaxPriorityFeePerGas.Value, UnitConversion.EthUnit.Gwei)
                    .ToString("F2");
            }
            else if (suggestion.GasPrice.HasValue)
            {
                IsEip1559Enabled = false;
                CustomGasPrice = UnitConversion.Convert
                    .FromWei(suggestion.GasPrice.Value, UnitConversion.EthUnit.Gwei)
                    .ToString("F2");
            }
        }
        
        public void LoadBaseValues(GasPriceSuggestion suggestion)
        {
            BaseGasPrice = "";
            BaseMaxFee = "";
            BasePriorityFee = "";
            AdjustedGasPrice = "";
            AdjustedMaxFee = "";
            AdjustedPriorityFee = "";
            
            if (IsEip1559Enabled && suggestion.MaxFeePerGas.HasValue && suggestion.MaxPriorityFeePerGas.HasValue)
            {
                BaseMaxFee = UnitConversion.Convert
                    .FromWei(suggestion.MaxFeePerGas.Value, UnitConversion.EthUnit.Gwei)
                    .ToString("F2");
                BasePriorityFee = UnitConversion.Convert
                    .FromWei(suggestion.MaxPriorityFeePerGas.Value, UnitConversion.EthUnit.Gwei)
                    .ToString("F2");
            }
            else if (!IsEip1559Enabled && suggestion.GasPrice.HasValue)
            {
                BaseGasPrice = UnitConversion.Convert
                    .FromWei(suggestion.GasPrice.Value, UnitConversion.EthUnit.Gwei)
                    .ToString("F2");
            }
            
            RecalculateAdjustedValues();
        }
        
        public void ApplyMultiplier(decimal multiplier)
        {
            SelectedMultiplier = multiplier;
            IsCustomMode = false;
            RecalculateAdjustedValues();
        }
        
        public void EnableCustomMode()
        {
            IsCustomMode = true;
            SelectedMultiplier = 0;
        }
        
        public void ClearModeSpecificValues()
        {
            if (IsEip1559Enabled)
            {
                BaseGasPrice = "";
                AdjustedGasPrice = "";
                CustomGasPrice = "";
            }
            else
            {
                BaseMaxFee = "";
                BasePriorityFee = "";
                AdjustedMaxFee = "";
                AdjustedPriorityFee = "";
                CustomMaxFee = "";
                CustomPriorityFee = "";
            }
        }
        
        private void RecalculateAdjustedValues()
        {
            if (IsCustomMode) return;

            if (IsEip1559Enabled)
            {
                if (decimal.TryParse(BaseMaxFee, out var maxFee))
                    AdjustedMaxFee = (maxFee * SelectedMultiplier).ToString("F2");
                if (decimal.TryParse(BasePriorityFee, out var priorityFee))
                    AdjustedPriorityFee = (priorityFee * SelectedMultiplier).ToString("F2");
            }
            else
            {
                if (decimal.TryParse(BaseGasPrice, out var gasPrice))
                    AdjustedGasPrice = (gasPrice * SelectedMultiplier).ToString("F2");
            }
        }
        
        public void ValidateAll()
        {
            ValidateGasLimit();
            if (IsEip1559Enabled)
            {
                ValidateMaxFee();
                ValidatePriorityFee();
            }
            else
            {
                ValidateGasPrice();
            }
        }
        
        public void Reset()
        {
            Strategy = GasStrategy.Normal;
            CustomGasLimit = TransactionConstants.DEFAULT_TRANSFER_GAS_LIMIT.ToString();
            CustomGasPrice = "";
            CustomMaxFee = "";
            CustomPriorityFee = "";
            IsEip1559Enabled = false;
            ClearErrors();
        }
    }
}