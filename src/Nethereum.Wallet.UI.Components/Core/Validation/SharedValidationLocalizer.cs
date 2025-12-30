using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Core.Validation
{
    public class SharedValidationLocalizer : ComponentLocalizerBase<object>
    {
        public static class Keys
        {
            // Keys that match validation attribute error messages exactly
            public const string InvalidEthereumAddress = "Invalid Ethereum address";
            public const string AddressCannotBeNull = "Address cannot be null";
            public const string FieldIsRequired = "The field is required";
            
            // Hex validations (from HexAttribute)
            public const string HexValueCannotBeNull = "Hex value cannot be null";
            public const string InvalidHexValue = "Invalid hex value";
            
            public const string AmountMustBeValidNumber = "Amount must be a valid number";
            public const string AmountMustBePositive = "Amount must be positive";
            public const string InsufficientBalance = "Insufficient balance";
            public const string ValueOutOfRange = "The field must be between {0} and {1}";
            
            public const string GasLimitTooLow = "Gas limit is too low";
            public const string InvalidGasValue = "Invalid gas value";
            
            public const string DataMustStartWith0x = "Data must start with 0x";
            public const string DataMustBeHex = "Data must be valid hexadecimal";
            public const string DataMustHaveEvenChars = "Hex data must have even number of characters";
            
            public const string NonceMustBeNonNegative = "Nonce must be non-negative";
            
            public const string InvalidUrl = "InvalidUrl";
            public const string InvalidRpcUrl = "InvalidRpcUrl";
            public const string InvalidChainId = "InvalidChainId";
            public const string NetworkUnreachable = "NetworkUnreachable";
            
            public const string GasLimitTooHigh = "GasLimitTooHigh";
            public const string GasPriceTooLow = "GasPriceTooLow";
            public const string InvalidGasLimit = "InvalidGasLimit";
            public const string InvalidGasPrice = "InvalidGasPrice";
            
            public const string InvalidNonce = "InvalidNonce";
            public const string NonceTooLow = "NonceTooLow";
            
            public const string FieldRequired = "The field is required";
            public const string ValueMustBeNonNegative = "Value must be non-negative";
        }
        
        public SharedValidationLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                // Validation attribute error messages (keys match exactly)
                [Keys.InvalidEthereumAddress] = "Invalid Ethereum address",
                [Keys.AddressCannotBeNull] = "Address cannot be null", 
                [Keys.FieldIsRequired] = "This field is required",
                
                [Keys.HexValueCannotBeNull] = "Hex value cannot be null",
                [Keys.InvalidHexValue] = "Invalid hex value",
                
                [Keys.AmountMustBeValidNumber] = "Amount must be a valid number",
                [Keys.AmountMustBePositive] = "Amount must be positive",
                [Keys.InsufficientBalance] = "Insufficient balance",
                [Keys.ValueOutOfRange] = "The field must be between {0} and {1}",
                
                [Keys.GasLimitTooLow] = "Gas limit is too low (minimum 21000)",
                [Keys.InvalidGasValue] = "Invalid gas value",
                
                [Keys.DataMustStartWith0x] = "Data must start with 0x",
                [Keys.DataMustBeHex] = "Data must be valid hexadecimal", 
                [Keys.DataMustHaveEvenChars] = "Hex data must have even number of characters",
                
                [Keys.NonceMustBeNonNegative] = "Nonce must be non-negative",
                
                [Keys.InvalidUrl] = "Invalid URL format",
                [Keys.InvalidRpcUrl] = "Invalid RPC endpoint URL",
                [Keys.InvalidChainId] = "Invalid chain ID",
                [Keys.NetworkUnreachable] = "Chain is unreachable",
                
                [Keys.GasLimitTooLow] = "Gas limit is too low (minimum 21000)",
                [Keys.GasLimitTooHigh] = "Gas limit is too high",
                [Keys.GasPriceTooLow] = "Gas price is too low",
                [Keys.InvalidGasLimit] = "Invalid gas limit",
                [Keys.InvalidGasPrice] = "Invalid gas price",
                
                [Keys.DataMustStartWith0x] = "Data must start with 0x",
                [Keys.DataMustBeHex] = "Data must be valid hexadecimal",
                [Keys.DataMustHaveEvenChars] = "Hex data must have even number of characters",
                
                [Keys.InvalidNonce] = "Invalid nonce value",
                [Keys.NonceTooLow] = "Nonce is too low",
                [Keys.NonceMustBeNonNegative] = "Nonce must be non-negative",
                
                [Keys.FieldRequired] = "This field is required",
                [Keys.ValueMustBeNonNegative] = "Value must be non-negative"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.InvalidEthereumAddress] = "Dirección Ethereum inválida",
                [Keys.AddressCannotBeNull] = "La dirección no puede ser nula",
                [Keys.FieldIsRequired] = "Este campo es obligatorio",
                
                [Keys.HexValueCannotBeNull] = "El valor hexadecimal no puede ser nulo",
                [Keys.InvalidHexValue] = "Valor hexadecimal inválido",
                
                [Keys.AmountMustBeValidNumber] = "La cantidad debe ser un número válido",
                [Keys.AmountMustBePositive] = "La cantidad debe ser positiva", 
                [Keys.InsufficientBalance] = "Saldo insuficiente",
                [Keys.ValueOutOfRange] = "El campo debe estar entre {0} y {1}",
                
                [Keys.GasLimitTooLow] = "El límite de gas es demasiado bajo (mínimo 21000)",
                [Keys.InvalidGasValue] = "Valor de gas inválido",
                
                [Keys.DataMustStartWith0x] = "Los datos deben comenzar con 0x",
                [Keys.DataMustBeHex] = "Los datos deben ser hexadecimales válidos",
                [Keys.DataMustHaveEvenChars] = "Los datos hex deben tener un número par de caracteres",
                
                [Keys.NonceMustBeNonNegative] = "El nonce debe ser no negativo",
                
                [Keys.InvalidUrl] = "Formato de URL inválido",
                [Keys.InvalidRpcUrl] = "URL del endpoint RPC inválida",
                [Keys.InvalidChainId] = "ID de cadena inválido",
                [Keys.NetworkUnreachable] = "La cadena no es accesible",
                
                [Keys.GasLimitTooLow] = "El límite de gas es demasiado bajo (mínimo 21000)",
                [Keys.GasLimitTooHigh] = "El límite de gas es demasiado alto",
                [Keys.GasPriceTooLow] = "El precio del gas es demasiado bajo",
                [Keys.InvalidGasLimit] = "Límite de gas inválido",
                [Keys.InvalidGasPrice] = "Precio de gas inválido",
                
                [Keys.DataMustStartWith0x] = "Los datos deben comenzar con 0x",
                [Keys.DataMustBeHex] = "Los datos deben ser hexadecimales válidos",
                [Keys.DataMustHaveEvenChars] = "Los datos hex deben tener un número par de caracteres",
                
                [Keys.InvalidNonce] = "Valor de nonce inválido",
                [Keys.NonceTooLow] = "El nonce es demasiado bajo",
                [Keys.NonceMustBeNonNegative] = "El nonce debe ser no negativo",
                
                [Keys.FieldRequired] = "Este campo es obligatorio",
                [Keys.ValueMustBeNonNegative] = "El valor debe ser no negativo"
            });
        }
    }
}