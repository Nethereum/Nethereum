using System;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.UI.Validation.Attributes;
using Nethereum.Util;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Validation;
using static Nethereum.Wallet.UI.Components.Core.Validation.SharedValidationLocalizer;

namespace Nethereum.Wallet.UI.Components.SendTransaction.Models
{
    [Function("transfer", "bool")]
    public class ERC20TransferFunction : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public string To { get; set; }

        [Parameter("uint256", "_value", 2)]
        public BigInteger Value { get; set; }
    }

    public partial class TokenNativeTransferModel : LocalizedValidationModel
    {
        public TokenNativeTransferModel(IComponentLocalizer localizer) : base(localizer)
        {
            _tokenSymbol = "ETH";
            _tokenDecimals = 18;
            _isNativeToken = true;
        }

        [ObservableProperty] private string _fromAddress = "";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "The field is required")]
        [EthereumAddress(ErrorMessage = "Invalid Ethereum address")]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _recipientAddress = "";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "The field is required")]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _amount = "";

        [ObservableProperty] private BigInteger _availableBalance = BigInteger.Zero;
        [ObservableProperty] private string _tokenSymbol;
        [ObservableProperty] private int _tokenDecimals;
        [ObservableProperty] private bool _showAdvancedOptions = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private bool _isNativeToken = true;

        [ObservableProperty] private string? _contractAddress;
        [ObservableProperty] private string? _tokenLogoUri;
        [ObservableProperty] private long _chainId;
        
        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Hex(ErrorMessage = "Invalid hex value")]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _transactionData = "";
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _nonce = "";
        
        public decimal AmountValue => decimal.TryParse(Amount, out var amt) ? amt : 0;
        
        public BigInteger GetTransferAmountInSmallestUnit() => 
            UnitConversion.Convert.ToWei(AmountValue, TokenDecimals);
        
        public string FormattedAvailableBalance
        {
            get
            {
                if (AvailableBalance == BigInteger.Zero)
                    return "0";
                    
                var tokenValue = UnitConversion.Convert.FromWei(AvailableBalance, TokenDecimals);
                
                if (tokenValue >= 1000)
                    return tokenValue.ToString("N2");
                else if (tokenValue >= 1)
                    return tokenValue.ToString("F4").TrimEnd('0').TrimEnd('.');
                else if (tokenValue >= 0.001m)
                    return tokenValue.ToString("F6").TrimEnd('0').TrimEnd('.');
                else
                    return tokenValue.ToString("F8").TrimEnd('0').TrimEnd('.');
            }
        }
        
        public bool IsValid => 
            !HasErrors && 
            !string.IsNullOrWhiteSpace(RecipientAddress) &&
            !string.IsNullOrWhiteSpace(Amount) &&
            ValidateAmountBalance();
        
        partial void OnRecipientAddressChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Equals(FromAddress, StringComparison.OrdinalIgnoreCase))
            {
                ClearCustomErrors(nameof(RecipientAddress));
                AddCustomError("CannotSendToSelf", nameof(RecipientAddress));
            }
            else
            {
                ClearCustomErrors(nameof(RecipientAddress));
            }
            OnPropertyChanged(nameof(IsValid));
        }
        
        partial void OnAmountChanged(string value)
        {
            ValidateAmountValue();
            ValidateAmountBalance();
            OnPropertyChanged(nameof(IsValid));
        }
        
        partial void OnAvailableBalanceChanged(BigInteger value)
        {
            ValidateAmountBalance();
        }
        
        partial void OnTokenSymbolChanged(string value) { }
        
        partial void OnTokenDecimalsChanged(int value)
        {
            ValidateAmountBalance();
        }
        
        partial void OnNonceChanged(string value)
        {
            // Custom validation for nonce (must be non-negative BigInteger)
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (!BigInteger.TryParse(value, out var n) || n < 0)
                {
                    ClearCustomErrors(nameof(Nonce));
                    AddCustomError(Keys.NonceMustBeNonNegative, nameof(Nonce));
                    return;
                }
            }
            ClearCustomErrors(nameof(Nonce));
        }
        
        private void ValidateAmountValue()
        {
            if (string.IsNullOrWhiteSpace(Amount))
            {
                ClearCustomErrors(nameof(Amount));
                return;
            }
            
            if (!decimal.TryParse(Amount, out var amt))
            {
                AddCustomError(Keys.AmountMustBeValidNumber, nameof(Amount));
                return;
            }
            
            if (amt <= 0)
            {
                AddCustomError(Keys.AmountMustBePositive, nameof(Amount));
                return;
            }
            
            ClearCustomErrors(nameof(Amount));
        }
        
        public bool ValidateAmountBalance()
        {
            if (string.IsNullOrWhiteSpace(Amount)) return false;
            if (!decimal.TryParse(Amount, out var amt) || amt <= 0) return false;
            
            if (GetTransferAmountInSmallestUnit() > AvailableBalance)
            {
                ClearCustomErrors(nameof(Amount));
                AddCustomError(Keys.InsufficientBalance, nameof(Amount));
                return false;
            }
            
            ClearCustomErrors(nameof(Amount));
            return true;
        }
        
        public void ValidateAll()
        {
            ValidateAllProperties();
            ValidateAmountValue();
            ValidateAmountBalance();
            
            OnPropertyChanged(nameof(RecipientAddress));
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(TransactionData));
            OnPropertyChanged(nameof(Nonce));
        }
        
        public void SetMaxAmount()
        {
            if (AvailableBalance > BigInteger.Zero)
            {
                var tokenValue = UnitConversion.Convert.FromWei(AvailableBalance, TokenDecimals);
                Amount = tokenValue.ToString();
            }
        }
        
        public void UpdateForNetwork(string symbol, int decimals)
        {
            TokenSymbol = symbol;
            TokenDecimals = decimals;
        }
        
        public void Reset()
        {
            RecipientAddress = "";
            Amount = "";
            TransactionData = "";
            Nonce = "";
            ShowAdvancedOptions = false;
            IsNativeToken = true;
            ContractAddress = null;
            TokenLogoUri = null;
            ChainId = 0;
            ClearErrors();
        }

        public string GetTransactionToAddress()
        {
            return IsNativeToken ? RecipientAddress : ContractAddress ?? RecipientAddress;
        }

        public BigInteger GetTransactionValue()
        {
            return IsNativeToken ? GetTransferAmountInSmallestUnit() : BigInteger.Zero;
        }

        public string? GetEncodedTransferData()
        {
            if (IsNativeToken)
            {
                return string.IsNullOrWhiteSpace(TransactionData) ? null : TransactionData;
            }

            var transferFunction = new ERC20TransferFunction
            {
                To = RecipientAddress,
                Value = GetTransferAmountInSmallestUnit()
            };
            return transferFunction.GetCallData().ToHex(true);
        }

        public void SetToken(string symbol, int decimals, bool isNative, string? contractAddress = null, string? logoUri = null, long chainId = 0)
        {
            TokenSymbol = symbol;
            TokenDecimals = decimals;
            IsNativeToken = isNative;
            ContractAddress = contractAddress;
            TokenLogoUri = logoUri;
            ChainId = chainId;
            AvailableBalance = BigInteger.Zero;
            Amount = "";
            ClearCustomErrors(nameof(Amount));
            OnPropertyChanged(nameof(IsValid));
        }
    }
}