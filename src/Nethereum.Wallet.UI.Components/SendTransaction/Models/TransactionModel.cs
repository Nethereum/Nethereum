using System;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.UI.Validation.Attributes;
using Nethereum.Util;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Core.Validation;
using Nethereum.Wallet.UI.Components.SendTransaction.Components;

namespace Nethereum.Wallet.UI.Components.SendTransaction.Models
{
    public partial class TransactionModel : LocalizedValidationModel
    {
        public TransactionModel(IComponentLocalizer localizer) : base(localizer)
        {
            _gasConfiguration = new GasConfigurationModel(localizer);
            
            _gasConfiguration.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GasConfigurationModel.IsValid))
                {
                    OnPropertyChanged(nameof(IsValid));
                }
            };
        }

        [ObservableProperty] private string _fromAddress = "";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [EthereumAddress(ErrorMessage = "Invalid Ethereum address")]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _recipientAddress = "";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Required(ErrorMessage = "AmountRequired")]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _amount = "";

        [ObservableProperty]
        [NotifyDataErrorInfo]
        [Hex(ErrorMessage = "Invalid hex value")]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _data = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string _nonce = "";

        [ObservableProperty] private GasConfigurationModel _gasConfiguration;

        [ObservableProperty] private BigInteger _chainId = BigInteger.One;

        public bool IsValid => !HasErrors;

        partial void OnRecipientAddressChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && 
                !string.IsNullOrWhiteSpace(FromAddress) && 
                value.Equals(FromAddress, StringComparison.OrdinalIgnoreCase))
            {
                ClearCustomErrors(nameof(RecipientAddress));
                AddCustomError(TransactionLocalizer.Keys.CannotSendToSelf, nameof(RecipientAddress));
            }
            else
            {
                ClearCustomErrors(nameof(RecipientAddress));
            }
        }

        partial void OnAmountChanged(string value)
        {
            ValidateAmount();
        }

        partial void OnDataChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("0x"))
            {
                ClearCustomErrors(nameof(Data));
                AddCustomError(TransactionLocalizer.Keys.DataMustStartWith0x, nameof(Data));
            }
            else
            {
                ClearCustomErrors(nameof(Data));
            }
        }

        partial void OnNonceChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (!BigInteger.TryParse(value, out var n) || n < 0)
                {
                    ClearCustomErrors(nameof(Nonce));
                    AddCustomError(TransactionLocalizer.Keys.NonceMustBeNonNegative, nameof(Nonce));
                }
                else
                {
                    ClearCustomErrors(nameof(Nonce));
                }
            }
            else
            {
                ClearCustomErrors(nameof(Nonce));
            }
        }

        private void ValidateAmount()
        {
            if (!string.IsNullOrWhiteSpace(Amount))
            {
                if (!decimal.TryParse(Amount, out var amt))
                {
                    ClearCustomErrors(nameof(Amount));
                    AddCustomError(SharedValidationLocalizer.Keys.AmountMustBeValidNumber, nameof(Amount));
                }
                else if (amt < 0)
                {
                    ClearCustomErrors(nameof(Amount));
                    AddCustomError(SharedValidationLocalizer.Keys.AmountMustBePositive, nameof(Amount));
                }
                else
                {
                    ClearCustomErrors(nameof(Amount));
                }
            }
            else
            {
                ClearCustomErrors(nameof(Amount));
            }
        }

        public BigInteger GetAmountInWei(int decimals = 18)
        {
            if (string.IsNullOrWhiteSpace(Amount)) return BigInteger.Zero;
            if (!decimal.TryParse(Amount, out var amt)) return BigInteger.Zero;
            return UnitConversion.Convert.ToWei(amt, decimals);
        }

        public TransactionInput? BuildTransactionInput(int tokenDecimals = 18)
        {
            if (!IsValid) return null;

            try
            {
                var transactionInput = new TransactionInput
                {
                    From = FromAddress,
                    To = RecipientAddress,
                    Value = new HexBigInteger(GetAmountInWei(tokenDecimals)),
                    Data = string.IsNullOrWhiteSpace(Data) ? null : Data
                };

                if (GasConfiguration?.GasLimitValue != null && GasConfiguration.GasLimitValue > 0)
                {
                    transactionInput.Gas = new HexBigInteger(GasConfiguration.GasLimitValue);
                }

                if (GasConfiguration?.IsEip1559Enabled == true)
                {
                    if (GasConfiguration.MaxFeeValue > 0)
                        transactionInput.MaxFeePerGas = new HexBigInteger(GasConfiguration.MaxFeeValue);
                    if (GasConfiguration.PriorityFeeValue > 0)
                        transactionInput.MaxPriorityFeePerGas = new HexBigInteger(GasConfiguration.PriorityFeeValue);
                }
                else
                {
                    if (GasConfiguration?.GasPriceValue > 0)
                        transactionInput.GasPrice = new HexBigInteger(GasConfiguration.GasPriceValue);
                }

                if (!string.IsNullOrWhiteSpace(Nonce) && BigInteger.TryParse(Nonce, out var nonceValue))
                {
                    transactionInput.Nonce = new HexBigInteger(nonceValue);
                }

                return transactionInput;
            }
            catch
            {
                return null;
            }
        }

        public void ValidateAll()
        {
            ValidateAllProperties();
            ValidateAmount();
            GasConfiguration?.ValidateAll();
            
            OnPropertyChanged(nameof(RecipientAddress));
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(Data));
            OnPropertyChanged(nameof(Nonce));
        }

        public void Reset()
        {
            RecipientAddress = "";
            Amount = "";
            Data = "";
            Nonce = "";
            GasConfiguration?.Reset();
            ClearErrors();
        }

        public bool HasError(string propertyName) => HasFieldErrors(propertyName);
        
        public string? GetError(string propertyName) => GetFieldError(propertyName);
    }
}