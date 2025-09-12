using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public class NetworkDetailsLocalizer : ComponentLocalizerBase<NetworkDetailsViewModel>
    {
        public static class Keys
        {
            public const string NetworkDetails = "NetworkDetails";
            public const string NetworkOverview = "NetworkOverview";
            public const string NetworkName = "NetworkName";
            public const string SelectNetwork = "SelectNetwork";
            public const string ChainId = "ChainId";
            public const string Currency = "Currency";
            public const string RpcEndpoints = "RpcEndpoints";
            public const string BlockExplorers = "BlockExplorers";
            public const string RpcConfiguration = "RpcConfiguration";
            public const string Advanced = "Advanced";
            public const string TestConnection = "TestConnection";
            public const string RpcUrl = "RpcUrl";
            public const string RpcUrlPlaceholder = "RpcUrlPlaceholder";
            public const string AddRpcEndpoint = "AddRpcEndpoint";
            public const string RefreshFromChainList = "RefreshFromChainList";
            public const string RemoveRpcEndpoint = "RemoveRpcEndpoint";
            public const string TestRpcEndpoint = "TestRpcEndpoint";
            public const string NoRpcEndpoints = "NoRpcEndpoints";
            public const string LoadingNetwork = "LoadingNetwork";
            public const string NetworkNotFound = "NetworkNotFound";
            public const string Back = "Back";
            public const string BackToNetworks = "BackToNetworks";
            public const string Continue = "Continue";
            public const string Save = "Save";
            public const string Active = "Active";
            public const string Loading = "Loading";
            public const string EditNetwork = "EditNetwork";
            public const string CurrencySettings = "CurrencySettings";
            public const string CurrencySymbol = "CurrencySymbol";
            public const string CurrencyName = "CurrencyName";
            public const string CurrencyDecimals = "CurrencyDecimals";
            public const string NetworkType = "NetworkType";
            public const string TestnetNetwork = "TestnetNetwork";
            public const string ProtocolSupport = "ProtocolSupport";
            public const string Eip155Support = "Eip155Support";
            public const string Eip1559Support = "Eip1559Support";
            public const string ExplorerUrl = "ExplorerUrl";
            public const string NewExplorerUrl = "NewExplorerUrl";
            public const string AddExplorer = "AddExplorer";
            public const string NoExplorers = "NoExplorers";
            public const string SaveChanges = "SaveChanges";
            public const string OpenExplorerTooltip = "OpenExplorerTooltip";
            public const string RemoveExplorerTooltip = "RemoveExplorerTooltip";
            
            public const string Inactive = "Inactive";
            public const string NetworkStatus = "NetworkStatus";
            public const string NetworkStatusDescription = "NetworkStatusDescription";
            public const string ActivateNetwork = "ActivateNetwork";
            public const string DeactivateNetwork = "DeactivateNetwork";
            public const string RpcSelectionStrategy = "RpcSelectionStrategy";
            public const string RpcSelectionStrategyDescription = "RpcSelectionStrategyDescription";
            public const string SelectionStrategy = "SelectionStrategy";
            public const string DangerZone = "DangerZone";
            public const string DangerZoneDescription = "DangerZoneDescription";
            public const string ResetNetwork = "ResetNetwork";
            public const string ResetNetworkDescription = "ResetNetworkDescription";
            public const string Reset = "Reset";
            public const string RemoveNetwork = "RemoveNetwork";
            public const string RemoveNetworkDescription = "RemoveNetworkDescription";
            public const string Remove = "Remove";
            public const string CoreNetwork = "CoreNetwork";
            public const string Error = "Error";
            public const string Success = "Success";
            public const string NetworkNotFoundDescription = "NetworkNotFoundDescription";
            
            public const string Single = "Single";
            public const string RandomMultiple = "RandomMultiple";
            public const string LoadBalanced = "LoadBalanced";
            
            public const string SingleDescription = "SingleDescription";
            public const string RandomMultipleDescription = "RandomMultipleDescription";
            public const string LoadBalancedDescription = "LoadBalancedDescription";
            public const string DefaultStrategyDescription = "DefaultStrategyDescription";
            
            public const string EditNetworkDescription = "EditNetworkDescription";
            public const string RpcConfigurationDescription = "RpcConfigurationDescription";
            public const string AdvancedDescription = "AdvancedDescription";
            
            public const string AddRpcEndpointsDescription = "AddRpcEndpointsDescription";
            public const string Custom = "Custom";
            public const string TestAllRpcs = "TestAllRpcs";
            public const string RemoveFailedRpcs = "RemoveFailedRpcs";
            public const string SaveRpcConfiguration = "SaveRpcConfiguration";
            public const string SingleRpcMode = "SingleRpcMode";
            public const string MultipleRpcMode = "MultipleRpcMode";
            public const string SingleRpcModeDescription = "SingleRpcModeDescription";
            public const string MultipleRpcModeDescription = "MultipleRpcModeDescription";
            
            public const string NetworkNotFoundError = "NetworkNotFoundError";
            public const string FailedToLoadNetwork = "FailedToLoadNetwork";
            public const string NetworkActivatedSuccessfully = "NetworkActivatedSuccessfully";
            public const string FailedToActivateNetwork = "FailedToActivateNetwork";
            public const string NetworkDeactivatedSuccessfully = "NetworkDeactivatedSuccessfully";
            public const string FailedToDeactivateNetwork = "FailedToDeactivateNetwork";
            public const string NetworkRemovedSuccessfully = "NetworkRemovedSuccessfully";
            public const string FailedToRemoveNetwork = "FailedToRemoveNetwork";
            public const string CannotRemoveCoreNetwork = "CannotRemoveCoreNetwork";
            public const string NetworkResetSuccessfully = "NetworkResetSuccessfully";
            public const string FailedToResetNetwork = "FailedToResetNetwork";
            public const string NoDefaultConfiguration = "NoDefaultConfiguration";
            public const string NetworkSelectedSuccessfully = "NetworkSelectedSuccessfully";
            public const string FailedToSelectNetwork = "FailedToSelectNetwork";
            
            public const string FormValidationFailed = "FormValidationFailed";
            public const string NetworkNameRequired = "NetworkNameRequired";
            public const string NetworkNameTooShort = "NetworkNameTooShort";
            public const string CurrencySymbolRequired = "CurrencySymbolRequired";
            public const string InvalidCurrencySymbol = "InvalidCurrencySymbol";
            public const string CurrencyNameRequired = "CurrencyNameRequired";
            public const string CurrencyNameTooShort = "CurrencyNameTooShort";
            public const string InvalidCurrencyDecimals = "InvalidCurrencyDecimals";
            public const string RpcUrlRequired = "RpcUrlRequired";
            public const string InvalidRpcUrlFormat = "InvalidRpcUrlFormat";
            public const string DuplicateRpcUrl = "DuplicateRpcUrl";
            public const string NetworkUpdatedSuccessfully = "NetworkUpdatedSuccessfully";
            public const string FailedToUpdateNetwork = "FailedToUpdateNetwork";
            public const string RpcEndpointAddedSuccessfully = "RpcEndpointAddedSuccessfully";
            public const string FailedToAddRpcEndpoint = "FailedToAddRpcEndpoint";
        }

        public NetworkDetailsLocalizer(IWalletLocalizationService localizationService) 
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.NetworkDetails] = "Network Details",
                [Keys.NetworkOverview] = "Network Overview",
                [Keys.NetworkName] = "Network Name",
                [Keys.SelectNetwork] = "Select Network",
                [Keys.ChainId] = "Chain ID",
                [Keys.Currency] = "Native Currency",
                [Keys.RpcEndpoints] = "RPC Endpoints",
                [Keys.BlockExplorers] = "Block Explorers",
                [Keys.RpcConfiguration] = "RPC Configuration",
                [Keys.Advanced] = "Advanced Settings",
                [Keys.TestConnection] = "Test Connection",
                [Keys.RpcUrl] = "RPC URL",
                [Keys.RpcUrlPlaceholder] = "https://rpc.example.com",
                [Keys.AddRpcEndpoint] = "Add RPC Endpoint",
                [Keys.RefreshFromChainList] = "Refresh from ChainList",
                [Keys.RemoveRpcEndpoint] = "Remove",
                [Keys.TestRpcEndpoint] = "Test",
                [Keys.NoRpcEndpoints] = "No RPC Endpoints",
                [Keys.LoadingNetwork] = "Loading network...",
                [Keys.NetworkNotFound] = "Network not found",
                [Keys.Back] = "Back",
                [Keys.BackToNetworks] = "Back to Networks",
                [Keys.Continue] = "Continue", 
                [Keys.Save] = "Save",
                [Keys.Active] = "Active",
                [Keys.Loading] = "Loading",
                [Keys.EditNetwork] = "Edit Network",
                [Keys.CurrencySettings] = "Currency Settings",
                [Keys.CurrencySymbol] = "Currency Symbol",
                [Keys.CurrencyName] = "Currency Name", 
                [Keys.CurrencyDecimals] = "Currency Decimals",
                [Keys.NetworkType] = "Network Type",
                [Keys.TestnetNetwork] = "Testnet Network",
                [Keys.ProtocolSupport] = "Protocol Support",
                [Keys.Eip155Support] = "EIP-155 Support (Replay Protection)",
                [Keys.Eip1559Support] = "EIP-1559 Support (Fee Market)",
                [Keys.ExplorerUrl] = "Explorer URL",
                [Keys.NewExplorerUrl] = "New Explorer URL",
                [Keys.AddExplorer] = "Add Explorer",
                [Keys.NoExplorers] = "No block explorers configured. Add one to browse transactions and addresses for this network.",
                [Keys.SaveChanges] = "Save Changes",
                [Keys.OpenExplorerTooltip] = "Open explorer in new tab",
                [Keys.RemoveExplorerTooltip] = "Remove explorer",
                
                [Keys.Inactive] = "Inactive",
                [Keys.NetworkStatus] = "Network Status",
                [Keys.NetworkStatusDescription] = "Control whether this network is available for use",
                [Keys.ActivateNetwork] = "Activate Network",
                [Keys.DeactivateNetwork] = "Deactivate Network",
                [Keys.RpcSelectionStrategy] = "RPC Selection Strategy",
                [Keys.RpcSelectionStrategyDescription] = "Choose how RPC endpoints are selected for this network",
                [Keys.SelectionStrategy] = "Selection Strategy",
                [Keys.DangerZone] = "Danger Zone",
                [Keys.DangerZoneDescription] = "These actions cannot be undone",
                [Keys.ResetNetwork] = "Reset Network",
                [Keys.ResetNetworkDescription] = "Reset this network to default settings",
                [Keys.Reset] = "Reset",
                [Keys.RemoveNetwork] = "Remove Network",
                [Keys.RemoveNetworkDescription] = "Permanently remove this network configuration",
                [Keys.Remove] = "Remove",
                [Keys.CoreNetwork] = "Core Network",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success",
                [Keys.NetworkNotFoundDescription] = "The requested network could not be found.",
                
                [Keys.Single] = "Single Endpoint",
                [Keys.RandomMultiple] = "Random Selection",
                [Keys.LoadBalanced] = "Load Balanced",
                
                [Keys.SingleDescription] = "Use one selected RPC endpoint",
                [Keys.RandomMultipleDescription] = "Randomly select from multiple endpoints",
                [Keys.LoadBalancedDescription] = "Round-robin between multiple endpoints",
                [Keys.DefaultStrategyDescription] = "Select how RPC endpoints are used",
                
                [Keys.EditNetworkDescription] = "Edit network details and configuration",
                [Keys.RpcConfigurationDescription] = "Manage RPC endpoints and connection settings",
                [Keys.AdvancedDescription] = "Advanced network management and danger zone",
                
                [Keys.AddRpcEndpointsDescription] = "Add RPC endpoints to connect to this network",
                [Keys.Custom] = "Custom",
                [Keys.TestAllRpcs] = "Test All RPCs",
                [Keys.RemoveFailedRpcs] = "Remove Failed RPCs",
                [Keys.SaveRpcConfiguration] = "Save RPC Configuration",
                [Keys.SingleRpcMode] = "Single RPC Mode:",
                [Keys.MultipleRpcMode] = "Multiple RPC Mode:",
                [Keys.SingleRpcModeDescription] = "Select one RPC endpoint to use for all requests.",
                [Keys.MultipleRpcModeDescription] = "Select multiple RPC endpoints for load balancing and failover. The wallet will automatically use the configured strategy.",
                
                [Keys.NetworkNotFoundError] = "Network not found",
                [Keys.FailedToLoadNetwork] = "Failed to load network: {0}",
                [Keys.NetworkActivatedSuccessfully] = "Network activated successfully",
                [Keys.FailedToActivateNetwork] = "Failed to activate network",
                [Keys.NetworkDeactivatedSuccessfully] = "Network deactivated successfully",
                [Keys.FailedToDeactivateNetwork] = "Failed to deactivate network",
                [Keys.NetworkRemovedSuccessfully] = "Network removed successfully",
                [Keys.FailedToRemoveNetwork] = "Failed to remove network",
                [Keys.CannotRemoveCoreNetwork] = "Cannot remove core network - use Reset instead",
                [Keys.NetworkResetSuccessfully] = "Network reset to default configuration",
                [Keys.FailedToResetNetwork] = "Failed to reset network: {0}",
                [Keys.NoDefaultConfiguration] = "Failed to reset network - no default configuration available",
                [Keys.NetworkSelectedSuccessfully] = "Network selected successfully",
                [Keys.FailedToSelectNetwork] = "Failed to select network",
                
                [Keys.FormValidationFailed] = "Please correct all validation errors before saving",
                [Keys.NetworkNameRequired] = "Network name is required",
                [Keys.NetworkNameTooShort] = "Network name must be at least 2 characters",
                [Keys.CurrencySymbolRequired] = "Currency symbol is required",
                [Keys.InvalidCurrencySymbol] = "Currency symbol must be 1-10 characters",
                [Keys.CurrencyNameRequired] = "Currency name is required",
                [Keys.CurrencyNameTooShort] = "Currency name must be at least 2 characters",
                [Keys.InvalidCurrencyDecimals] = "Decimals must be between 0 and 18",
                [Keys.RpcUrlRequired] = "RPC URL is required",
                [Keys.InvalidRpcUrlFormat] = "Please enter a valid HTTP/HTTPS/WS/WSS URL",
                [Keys.DuplicateRpcUrl] = "This RPC URL has already been added",
                [Keys.NetworkUpdatedSuccessfully] = "Network updated successfully",
                [Keys.FailedToUpdateNetwork] = "Failed to update network: {0}",
                [Keys.RpcEndpointAddedSuccessfully] = "RPC endpoint added successfully",
                [Keys.FailedToAddRpcEndpoint] = "Failed to add RPC endpoint: {0}"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.NetworkDetails] = "Detalles de Red",
                [Keys.NetworkOverview] = "Resumen de Red",
                [Keys.NetworkName] = "Nombre de Red",
                [Keys.SelectNetwork] = "Seleccionar Red",
                [Keys.ChainId] = "ID de Cadena",
                [Keys.Currency] = "Moneda Nativa",
                [Keys.RpcEndpoints] = "Endpoints RPC",
                [Keys.BlockExplorers] = "Exploradores de Bloques",
                [Keys.RpcConfiguration] = "Configuración RPC",
                [Keys.Advanced] = "Configuración Avanzada",
                [Keys.TestConnection] = "Probar Conexión",
                [Keys.RpcUrl] = "URL RPC",
                [Keys.RpcUrlPlaceholder] = "https://rpc.example.com",
                [Keys.AddRpcEndpoint] = "Agregar Endpoint RPC",
                [Keys.RefreshFromChainList] = "Actualizar desde ChainList",
                [Keys.RemoveRpcEndpoint] = "Eliminar",
                [Keys.TestRpcEndpoint] = "Probar",
                [Keys.NoRpcEndpoints] = "Sin Endpoints RPC",
                [Keys.LoadingNetwork] = "Cargando red...",
                [Keys.NetworkNotFound] = "Red no encontrada",
                [Keys.Back] = "Atrás",
                [Keys.BackToNetworks] = "Volver a Redes",
                [Keys.Continue] = "Continuar",
                [Keys.Save] = "Guardar",
                [Keys.Active] = "Activo",
                [Keys.Loading] = "Cargando",
                [Keys.EditNetwork] = "Editar Red",
                [Keys.CurrencySettings] = "Configuración de Moneda",
                [Keys.CurrencySymbol] = "Símbolo de Moneda",
                [Keys.CurrencyName] = "Nombre de Moneda",
                [Keys.CurrencyDecimals] = "Decimales de Moneda",
                [Keys.NetworkType] = "Tipo de Red",
                [Keys.TestnetNetwork] = "Red de Prueba",
                [Keys.ProtocolSupport] = "Soporte de Protocolo",
                [Keys.Eip155Support] = "Soporte EIP-155 (Protección de Repetición)",
                [Keys.Eip1559Support] = "Soporte EIP-1559 (Mercado de Tarifas)",
                [Keys.ExplorerUrl] = "URL del Explorador",
                [Keys.NewExplorerUrl] = "Nueva URL del Explorador",
                [Keys.AddExplorer] = "Agregar Explorador",
                [Keys.NoExplorers] = "No hay exploradores de bloques configurados. Agrega uno para navegar transacciones y direcciones de esta red.",
                [Keys.SaveChanges] = "Guardar Cambios",
                [Keys.OpenExplorerTooltip] = "Abrir explorador en nueva pestaña",
                [Keys.RemoveExplorerTooltip] = "Eliminar explorador",
                
                [Keys.Inactive] = "Inactivo",
                [Keys.NetworkStatus] = "Estado de Red",
                [Keys.NetworkStatusDescription] = "Controla si esta red está disponible para uso",
                [Keys.ActivateNetwork] = "Activar Red",
                [Keys.DeactivateNetwork] = "Desactivar Red",
                [Keys.RpcSelectionStrategy] = "Estrategia de Selección RPC",
                [Keys.RpcSelectionStrategyDescription] = "Elige cómo se seleccionan los endpoints RPC para esta red",
                [Keys.SelectionStrategy] = "Estrategia de Selección",
                [Keys.DangerZone] = "Zona de Peligro",
                [Keys.DangerZoneDescription] = "Estas acciones no se pueden deshacer",
                [Keys.ResetNetwork] = "Restablecer Red",
                [Keys.ResetNetworkDescription] = "Restablecer esta red a configuración predeterminada",
                [Keys.Reset] = "Restablecer",
                [Keys.RemoveNetwork] = "Eliminar Red",
                [Keys.RemoveNetworkDescription] = "Eliminar permanentemente esta configuración de red",
                [Keys.Remove] = "Eliminar",
                [Keys.CoreNetwork] = "Red Principal",
                [Keys.Error] = "Error",
                [Keys.Success] = "Éxito",
                [Keys.NetworkNotFoundDescription] = "No se pudo encontrar la red solicitada.",
                
                [Keys.Single] = "Endpoint Único",
                [Keys.RandomMultiple] = "Selección Aleatoria",
                [Keys.LoadBalanced] = "Equilibrio de Carga",
                
                [Keys.SingleDescription] = "Usar un endpoint RPC seleccionado",
                [Keys.RandomMultipleDescription] = "Seleccionar aleatoriamente de múltiples endpoints",
                [Keys.LoadBalancedDescription] = "Rotación entre múltiples endpoints",
                [Keys.DefaultStrategyDescription] = "Seleccionar cómo se usan los endpoints RPC",
                
                [Keys.EditNetworkDescription] = "Editar detalles y configuración de red",
                [Keys.RpcConfigurationDescription] = "Gestionar endpoints RPC y configuración de conexión",
                [Keys.AdvancedDescription] = "Gestión avanzada de red y zona de peligro",
                
                [Keys.AddRpcEndpointsDescription] = "Agregar endpoints RPC para conectarse a esta red",
                [Keys.Custom] = "Personalizado",
                [Keys.TestAllRpcs] = "Probar Todos los RPCs",
                [Keys.RemoveFailedRpcs] = "Eliminar RPCs Fallidos",
                [Keys.SaveRpcConfiguration] = "Guardar Configuración RPC",
                [Keys.SingleRpcMode] = "Modo RPC Único:",
                [Keys.MultipleRpcMode] = "Modo RPC Múltiple:",
                [Keys.SingleRpcModeDescription] = "Seleccionar un endpoint RPC para usar en todas las solicitudes.",
                [Keys.MultipleRpcModeDescription] = "Seleccionar múltiples endpoints RPC para equilibrio de carga y respaldo. La billetera usará automáticamente la estrategia configurada.",
                
                [Keys.NetworkNotFoundError] = "Red no encontrada",
                [Keys.FailedToLoadNetwork] = "Error al cargar red: {0}",
                [Keys.NetworkActivatedSuccessfully] = "Red activada exitosamente",
                [Keys.FailedToActivateNetwork] = "Error al activar red",
                [Keys.NetworkDeactivatedSuccessfully] = "Red desactivada exitosamente",
                [Keys.FailedToDeactivateNetwork] = "Error al desactivar red",
                [Keys.NetworkRemovedSuccessfully] = "Red eliminada exitosamente",
                [Keys.FailedToRemoveNetwork] = "Error al eliminar red",
                [Keys.CannotRemoveCoreNetwork] = "No se puede eliminar red principal - usar Restablecer en su lugar",
                [Keys.NetworkResetSuccessfully] = "Red restablecida a configuración predeterminada",
                [Keys.FailedToResetNetwork] = "Error al restablecer red: {0}",
                [Keys.NoDefaultConfiguration] = "Error al restablecer red - no hay configuración predeterminada disponible",
                [Keys.NetworkSelectedSuccessfully] = "Red seleccionada exitosamente",
                [Keys.FailedToSelectNetwork] = "Error al seleccionar red",
                
                [Keys.FormValidationFailed] = "Por favor corrija todos los errores de validación antes de guardar",
                [Keys.NetworkNameRequired] = "El nombre de red es requerido",
                [Keys.NetworkNameTooShort] = "El nombre de red debe tener al menos 2 caracteres",
                [Keys.CurrencySymbolRequired] = "El símbolo de moneda es requerido",
                [Keys.InvalidCurrencySymbol] = "El símbolo de moneda debe tener 1-10 caracteres",
                [Keys.CurrencyNameRequired] = "El nombre de moneda es requerido",
                [Keys.CurrencyNameTooShort] = "El nombre de moneda debe tener al menos 2 caracteres",
                [Keys.InvalidCurrencyDecimals] = "Los decimales deben estar entre 0 y 18",
                [Keys.RpcUrlRequired] = "La URL RPC es requerida",
                [Keys.InvalidRpcUrlFormat] = "Por favor ingrese una URL HTTP/HTTPS/WS/WSS válida",
                [Keys.DuplicateRpcUrl] = "Esta URL RPC ya ha sido agregada",
                [Keys.NetworkUpdatedSuccessfully] = "Red actualizada exitosamente",
                [Keys.FailedToUpdateNetwork] = "Error al actualizar la red: {0}",
                [Keys.RpcEndpointAddedSuccessfully] = "Endpoint RPC agregado exitosamente",
                [Keys.FailedToAddRpcEndpoint] = "Error al agregar endpoint RPC: {0}"
            });
        }
    }
}