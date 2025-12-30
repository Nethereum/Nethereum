using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public class AddCustomTokenLocalizer : ComponentLocalizerBase<AddCustomTokenViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string ContractAddress = "ContractAddress";
            public const string ContractAddressRequired = "ContractAddressRequired";
            public const string ContractAddressInvalid = "ContractAddressInvalid";
            public const string Symbol = "Symbol";
            public const string SymbolRequired = "SymbolRequired";
            public const string SymbolTooLong = "SymbolTooLong";
            public const string Name = "Name";
            public const string NameRequired = "NameRequired";
            public const string Decimals = "Decimals";
            public const string DecimalsRequired = "DecimalsRequired";
            public const string DecimalsInvalid = "DecimalsInvalid";
            public const string LogoUri = "LogoUri";
            public const string FetchMetadata = "FetchMetadata";
            public const string FetchMetadataFailed = "FetchMetadataFailed";
            public const string Save = "Save";
            public const string Cancel = "Cancel";
            public const string TokenAddedSuccess = "TokenAddedSuccess";
            public const string TokenAddFailed = "TokenAddFailed";
            public const string TokenInfoSection = "TokenInfoSection";
            public const string NetworkSection = "NetworkSection";
            public const string Error = "Error";
            public const string Success = "Success";
            public const string SymbolPlaceholder = "SymbolPlaceholder";
            public const string NamePlaceholder = "NamePlaceholder";
        }

        public AddCustomTokenLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Add Custom Token",
                [Keys.ContractAddress] = "Contract Address",
                [Keys.ContractAddressRequired] = "Contract address is required",
                [Keys.ContractAddressInvalid] = "Invalid contract address format",
                [Keys.Symbol] = "Symbol",
                [Keys.SymbolRequired] = "Symbol is required",
                [Keys.SymbolTooLong] = "Symbol must be 11 characters or less",
                [Keys.Name] = "Name",
                [Keys.NameRequired] = "Name is required",
                [Keys.Decimals] = "Decimals",
                [Keys.DecimalsRequired] = "Decimals is required",
                [Keys.DecimalsInvalid] = "Decimals must be between 0 and 18",
                [Keys.LogoUri] = "Logo URL (optional)",
                [Keys.FetchMetadata] = "Fetch Metadata",
                [Keys.FetchMetadataFailed] = "Failed to fetch token metadata: {0}",
                [Keys.Save] = "Add Token",
                [Keys.Cancel] = "Cancel",
                [Keys.TokenAddedSuccess] = "Token added successfully",
                [Keys.TokenAddFailed] = "Failed to add token",
                [Keys.TokenInfoSection] = "Token Information",
                [Keys.NetworkSection] = "Network",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success",
                [Keys.SymbolPlaceholder] = "ETH",
                [Keys.NamePlaceholder] = "Ethereum"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Agregar Token Personalizado",
                [Keys.ContractAddress] = "Dirección del Contrato",
                [Keys.ContractAddressRequired] = "La dirección del contrato es requerida",
                [Keys.ContractAddressInvalid] = "Formato de dirección inválido",
                [Keys.Symbol] = "Símbolo",
                [Keys.SymbolRequired] = "El símbolo es requerido",
                [Keys.SymbolTooLong] = "El símbolo debe tener 11 caracteres o menos",
                [Keys.Name] = "Nombre",
                [Keys.NameRequired] = "El nombre es requerido",
                [Keys.Decimals] = "Decimales",
                [Keys.DecimalsRequired] = "Los decimales son requeridos",
                [Keys.DecimalsInvalid] = "Los decimales deben estar entre 0 y 18",
                [Keys.LogoUri] = "URL del Logo (opcional)",
                [Keys.FetchMetadata] = "Obtener Metadatos",
                [Keys.FetchMetadataFailed] = "Error al obtener metadatos: {0}",
                [Keys.Save] = "Agregar Token",
                [Keys.Cancel] = "Cancelar",
                [Keys.TokenAddedSuccess] = "Token agregado exitosamente",
                [Keys.TokenAddFailed] = "Error al agregar token",
                [Keys.TokenInfoSection] = "Informacion del Token",
                [Keys.NetworkSection] = "Red",
                [Keys.Error] = "Error",
                [Keys.Success] = "Exito",
                [Keys.SymbolPlaceholder] = "ETH",
                [Keys.NamePlaceholder] = "Ethereum"
            });
        }
    }
}
