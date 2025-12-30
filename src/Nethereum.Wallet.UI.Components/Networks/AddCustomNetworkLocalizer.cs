using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public class AddCustomNetworkLocalizer : ComponentLocalizerBase<AddCustomNetworkViewModel>
    {
        public static class Keys
        {
            public const string AddCustomNetwork = "AddCustomNetwork";
            public const string BackToNetworks = "BackToNetworks";
            public const string CreateNetwork = "CreateNetwork";
            public const string Cancel = "Cancel";
            
            public const string BasicInformation = "BasicInformation";
            public const string BasicInformationDescription = "BasicInformationDescription";
            public const string ChainId = "ChainId";
            public const string ChainIdPlaceholder = "ChainIdPlaceholder";
            public const string ChainIdHelperText = "ChainIdHelperText";
            public const string NetworkName = "NetworkName";
            public const string NetworkNamePlaceholder = "NetworkNamePlaceholder";
            public const string NetworkNameHelperText = "NetworkNameHelperText";
            
            public const string CurrencyConfiguration = "CurrencyConfiguration";
            public const string CurrencyConfigurationDescription = "CurrencyConfigurationDescription";
            public const string CurrencySymbol = "CurrencySymbol";
            public const string CurrencySymbolPlaceholder = "CurrencySymbolPlaceholder";
            public const string CurrencyName = "CurrencyName";
            public const string CurrencyNamePlaceholder = "CurrencyNamePlaceholder";
            public const string CurrencyDecimals = "CurrencyDecimals";
            public const string CurrencyDecimalsHelperText = "CurrencyDecimalsHelperText";
            
            public const string RpcEndpoints = "RpcEndpoints";
            public const string RpcEndpointsDescription = "RpcEndpointsDescription";
            public const string NewRpcUrl = "NewRpcUrl";
            public const string NewRpcUrlPlaceholder = "NewRpcUrlPlaceholder";
            public const string AddRpcEndpoint = "AddRpcEndpoint";
            public const string TestRpc = "TestRpc";
            public const string RemoveRpc = "RemoveRpc";
            public const string NoRpcEndpoints = "NoRpcEndpoints";
            public const string AtLeastOneRpcRequired = "AtLeastOneRpcRequired";
            
            public const string BlockExplorers = "BlockExplorers";
            public const string BlockExplorersDescription = "BlockExplorersDescription";
            public const string NewExplorerUrl = "NewExplorerUrl";
            public const string NewExplorerUrlPlaceholder = "NewExplorerUrlPlaceholder";
            public const string AddExplorer = "AddExplorer";
            public const string RemoveExplorer = "RemoveExplorer";
            public const string NoExplorers = "NoExplorers";
            
            public const string AdvancedSettings = "AdvancedSettings";
            public const string AdvancedSettingsDescription = "AdvancedSettingsDescription";
            public const string IsTestnet = "IsTestnet";
            public const string IsTestnetDescription = "IsTestnetDescription";
            public const string SupportEip155 = "SupportEip155";
            public const string SupportEip155Description = "SupportEip155Description";
            public const string SupportEip1559 = "SupportEip1559";
            public const string SupportEip1559Description = "SupportEip1559Description";
            
            public const string Testing = "Testing";
            public const string TestSuccess = "TestSuccess";
            public const string TestFailed = "TestFailed";
            public const string TestError = "TestError";
            public const string Loading = "Loading";
            public const string Error = "Error";
            public const string Success = "Success";
            
            public const string FormValidationFailed = "FormValidationFailed";
            public const string InvalidChainId = "InvalidChainId";
            public const string ChainIdRequiredForTesting = "ChainIdRequiredForTesting";
            public const string InvalidRpcUrlFormat = "InvalidRpcUrlFormat";
            public const string DuplicateRpcUrl = "DuplicateRpcUrl";
            public const string InvalidExplorerUrlFormat = "InvalidExplorerUrlFormat";
            public const string DuplicateExplorerUrl = "DuplicateExplorerUrl";
            public const string NetworkAlreadyExists = "NetworkAlreadyExists";
            public const string NetworkAddedSuccessfully = "NetworkAddedSuccessfully";
            public const string FailedToAddNetwork = "FailedToAddNetwork";
            
            public const string ChainIdRequired = "ChainIdRequired";
            public const string NetworkNameRequired = "NetworkNameRequired";
            public const string CurrencySymbolRequired = "CurrencySymbolRequired";
            public const string CurrencyNameRequired = "CurrencyNameRequired";
            public const string RpcUrlRequired = "RpcUrlRequired";
            public const string ExplorerUrlRequired = "ExplorerUrlRequired";
            
            public const string NetworkNameTooShort = "NetworkNameTooShort";
            public const string InvalidCurrencySymbol = "InvalidCurrencySymbol";
            public const string CurrencyNameTooShort = "CurrencyNameTooShort";
            public const string InvalidCurrencyDecimals = "InvalidCurrencyDecimals";
        }

        public AddCustomNetworkLocalizer(IWalletLocalizationService localizationService) 
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.AddCustomNetwork] = "Add Custom Chain",
                [Keys.BackToNetworks] = "Back to Chains",
                [Keys.CreateNetwork] = "Create Chain",
                [Keys.Cancel] = "Cancel",

                [Keys.BasicInformation] = "Basic Information",
                [Keys.BasicInformationDescription] = "Configure the essential chain details",
                [Keys.ChainId] = "Chain ID",
                [Keys.ChainIdPlaceholder] = "1337",
                [Keys.ChainIdHelperText] = "Unique identifier for this chain",
                [Keys.NetworkName] = "Chain Name",
                [Keys.NetworkNamePlaceholder] = "My Custom Chain",
                [Keys.NetworkNameHelperText] = "A descriptive name for your chain",
                
                [Keys.CurrencyConfiguration] = "Native Currency",
                [Keys.CurrencyConfigurationDescription] = "Configure the native currency for this chain",
                [Keys.CurrencySymbol] = "Currency Symbol",
                [Keys.CurrencySymbolPlaceholder] = "ETH",
                [Keys.CurrencyName] = "Currency Name",
                [Keys.CurrencyNamePlaceholder] = "Ether",
                [Keys.CurrencyDecimals] = "Decimal Places",
                [Keys.CurrencyDecimalsHelperText] = "Number of decimal places (0-18)",
                
                [Keys.RpcEndpoints] = "RPC Endpoints",
                [Keys.RpcEndpointsDescription] = "Add RPC endpoints to connect to this chain",
                [Keys.NewRpcUrl] = "RPC URL",
                [Keys.NewRpcUrlPlaceholder] = "https://rpc.example.com",
                [Keys.AddRpcEndpoint] = "Add RPC",
                [Keys.TestRpc] = "Test",
                [Keys.RemoveRpc] = "Remove",
                [Keys.NoRpcEndpoints] = "No RPC endpoints added yet",
                [Keys.AtLeastOneRpcRequired] = "At least one RPC endpoint is required",
                
                [Keys.BlockExplorers] = "Block Explorers",
                [Keys.BlockExplorersDescription] = "Add block explorer URLs (optional)",
                [Keys.NewExplorerUrl] = "Explorer URL",
                [Keys.NewExplorerUrlPlaceholder] = "https://explorer.example.com",
                [Keys.AddExplorer] = "Add Explorer",
                [Keys.RemoveExplorer] = "Remove",
                [Keys.NoExplorers] = "No block explorers added",
                
                [Keys.AdvancedSettings] = "Advanced Settings",
                [Keys.AdvancedSettingsDescription] = "Protocol support and chain classification",
                [Keys.IsTestnet] = "Testnet Chain",
                [Keys.IsTestnetDescription] = "Mark this as a test chain",
                [Keys.SupportEip155] = "EIP-155 Support",
                [Keys.SupportEip155Description] = "Replay protection (recommended: enabled)",
                [Keys.SupportEip1559] = "EIP-1559 Support", 
                [Keys.SupportEip1559Description] = "Fee market mechanism (recommended: enabled)",
                
                [Keys.Testing] = "Testing...",
                [Keys.TestSuccess] = "✓ Connected",
                [Keys.TestFailed] = "✗ Connection failed",
                [Keys.TestError] = "✗ Error",
                [Keys.Loading] = "Loading...",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success",
                
                [Keys.FormValidationFailed] = "Please fill in all required fields correctly",
                [Keys.InvalidChainId] = "Chain ID must be a valid positive number",
                [Keys.ChainIdRequiredForTesting] = "Chain ID must be set before testing RPC endpoints",
                [Keys.InvalidRpcUrlFormat] = "Please enter a valid HTTP/HTTPS/WS/WSS URL",
                [Keys.DuplicateRpcUrl] = "This RPC URL has already been added",
                [Keys.InvalidExplorerUrlFormat] = "Please enter a valid HTTP/HTTPS URL for the explorer",
                [Keys.DuplicateExplorerUrl] = "This explorer URL has already been added",
                [Keys.NetworkAlreadyExists] = "A chain with this Chain ID already exists",
                [Keys.NetworkAddedSuccessfully] = "Chain added successfully!",
                [Keys.FailedToAddNetwork] = "Failed to add chain",

                [Keys.ChainIdRequired] = "Chain ID is required",
                [Keys.NetworkNameRequired] = "Chain name is required",
                [Keys.CurrencySymbolRequired] = "Currency symbol is required",
                [Keys.CurrencyNameRequired] = "Currency name is required",
                [Keys.RpcUrlRequired] = "RPC URL is required",
                [Keys.ExplorerUrlRequired] = "Explorer URL is required",
                
                [Keys.NetworkNameTooShort] = "Chain name must be at least 2 characters",
                [Keys.InvalidCurrencySymbol] = "Currency symbol must be 1-10 characters",
                [Keys.CurrencyNameTooShort] = "Currency name must be at least 2 characters",
                [Keys.InvalidCurrencyDecimals] = "Currency decimals must be between 0 and 18"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.AddCustomNetwork] = "Agregar Cadena Personalizada",
                [Keys.BackToNetworks] = "Volver a Cadenas",
                [Keys.CreateNetwork] = "Crear Cadena",
                [Keys.Cancel] = "Cancelar",

                [Keys.BasicInformation] = "Información Básica",
                [Keys.BasicInformationDescription] = "Configurar los detalles esenciales de la cadena",
                [Keys.ChainId] = "ID de Cadena",
                [Keys.ChainIdPlaceholder] = "1337",
                [Keys.ChainIdHelperText] = "Identificador único para esta cadena",
                [Keys.NetworkName] = "Nombre de Cadena",
                [Keys.NetworkNamePlaceholder] = "Mi Cadena Personalizada",
                [Keys.NetworkNameHelperText] = "Un nombre descriptivo para tu cadena",

                [Keys.CurrencyConfiguration] = "Moneda Nativa",
                [Keys.CurrencyConfigurationDescription] = "Configurar la moneda nativa para esta cadena",
                [Keys.CurrencySymbol] = "Símbolo de Moneda",
                [Keys.CurrencySymbolPlaceholder] = "ETH",
                [Keys.CurrencyName] = "Nombre de Moneda",
                [Keys.CurrencyNamePlaceholder] = "Ether",
                [Keys.CurrencyDecimals] = "Decimales",
                [Keys.CurrencyDecimalsHelperText] = "Número de decimales (0-18)",
                
                [Keys.RpcEndpoints] = "Endpoints RPC",
                [Keys.RpcEndpointsDescription] = "Agregar endpoints RPC para conectarse a esta cadena",
                [Keys.NewRpcUrl] = "URL RPC",
                [Keys.NewRpcUrlPlaceholder] = "https://rpc.example.com",
                [Keys.AddRpcEndpoint] = "Agregar RPC",
                [Keys.TestRpc] = "Probar",
                [Keys.RemoveRpc] = "Eliminar",
                [Keys.NoRpcEndpoints] = "No se han agregado endpoints RPC aún",
                [Keys.AtLeastOneRpcRequired] = "Se requiere al menos un endpoint RPC",
                
                [Keys.BlockExplorers] = "Exploradores de Bloques",
                [Keys.BlockExplorersDescription] = "Agregar URLs de exploradores de bloques (opcional)",
                [Keys.NewExplorerUrl] = "URL del Explorador",
                [Keys.NewExplorerUrlPlaceholder] = "https://explorer.example.com",
                [Keys.AddExplorer] = "Agregar Explorador",
                [Keys.RemoveExplorer] = "Eliminar",
                [Keys.NoExplorers] = "No se han agregado exploradores de bloques",
                
                [Keys.AdvancedSettings] = "Configuración Avanzada",
                [Keys.AdvancedSettingsDescription] = "Soporte de protocolo y clasificación de cadena",
                [Keys.IsTestnet] = "Cadena de Prueba",
                [Keys.IsTestnetDescription] = "Marcar como cadena de prueba",
                [Keys.SupportEip155] = "Soporte EIP-155",
                [Keys.SupportEip155Description] = "Protección de repetición (recomendado: habilitado)",
                [Keys.SupportEip1559] = "Soporte EIP-1559",
                [Keys.SupportEip1559Description] = "Mecanismo de mercado de tarifas (recomendado: habilitado)",
                
                [Keys.Testing] = "Probando...",
                [Keys.TestSuccess] = "✓ Conectado",
                [Keys.TestFailed] = "✗ Conexión fallida",
                [Keys.TestError] = "✗ Error",
                [Keys.Loading] = "Cargando...",
                [Keys.Error] = "Error",
                [Keys.Success] = "Éxito",
                
                [Keys.FormValidationFailed] = "Por favor completa todos los campos requeridos correctamente",
                [Keys.InvalidChainId] = "El ID de cadena debe ser un número positivo válido",
                [Keys.ChainIdRequiredForTesting] = "El ID de cadena debe establecerse antes de probar endpoints RPC",
                [Keys.InvalidRpcUrlFormat] = "Por favor ingresa una URL HTTP/HTTPS/WS/WSS válida",
                [Keys.DuplicateRpcUrl] = "Esta URL RPC ya ha sido agregada",
                [Keys.InvalidExplorerUrlFormat] = "Por favor ingresa una URL HTTP/HTTPS válida para el explorador",
                [Keys.DuplicateExplorerUrl] = "Esta URL del explorador ya ha sido agregada",
                [Keys.NetworkAlreadyExists] = "Ya existe una cadena con este ID de cadena",
                [Keys.NetworkAddedSuccessfully] = "¡Cadena agregada exitosamente!",
                [Keys.FailedToAddNetwork] = "Error al agregar la cadena",

                [Keys.ChainIdRequired] = "El ID de cadena es requerido",
                [Keys.NetworkNameRequired] = "El nombre de cadena es requerido",
                [Keys.CurrencySymbolRequired] = "El símbolo de moneda es requerido",
                [Keys.CurrencyNameRequired] = "El nombre de moneda es requerido",
                [Keys.RpcUrlRequired] = "La URL RPC es requerida",
                [Keys.ExplorerUrlRequired] = "La URL del explorador es requerida",
                
                [Keys.NetworkNameTooShort] = "El nombre de cadena debe tener al menos 2 caracteres",
                [Keys.InvalidCurrencySymbol] = "El símbolo de moneda debe tener 1-10 caracteres",
                [Keys.CurrencyNameTooShort] = "El nombre de moneda debe tener al menos 2 caracteres",
                [Keys.InvalidCurrencyDecimals] = "Los decimales de moneda deben estar entre 0 y 18"
            });
        }
    }
}