using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public class NetworkListLocalizer : ComponentLocalizerBase<NetworkListViewModel>
    {
        public static class Keys
        {
            public const string NetworkList = "NetworkList";
            public const string SelectNetwork = "SelectNetwork";
            public const string SearchNetworks = "SearchNetworks";
            public const string ShowTestnets = "ShowTestnets";
            public const string RefreshNetworks = "RefreshNetworks";
            public const string LoadingNetworks = "LoadingNetworks";
            public const string NoNetworksFound = "NoNetworksFound";
            public const string NetworkDetails = "NetworkDetails";
            public const string Back = "Back";
            public const string SelectButton = "SelectButton";
            public const string Details = "Details";
            public const string AddCustomNetwork = "AddCustomNetwork";
            public const string ChainlistResultsTitle = "ChainlistResultsTitle";
            public const string ChainlistResultsDescription = "ChainlistResultsDescription";
            public const string ChainlistSearching = "ChainlistSearching";
            public const string ChainlistNoResults = "ChainlistNoResults";
            public const string ChainlistAddButton = "ChainlistAddButton";
            public const string ChainlistAddError = "ChainlistAddError";
            public const string ChainlistError = "ChainlistError";
            public const string ChainlistNoRpcEndpoints = "ChainlistNoRpcEndpoints";
            public const string ChainlistRpcCountLabel = "ChainlistRpcCountLabel";
            public const string AdjustSearchOrFilters = "AdjustSearchOrFilters";
            public const string ChainlistSupplementalDescription = "ChainlistSupplementalDescription";
            public const string ChainlistAlreadyAdded = "ChainlistAlreadyAdded";
            public const string InternalResultsTitle = "InternalResultsTitle";
            public const string InternalResultsDescription = "InternalResultsDescription";
            public const string InternalAddButton = "InternalAddButton";
            public const string Error = "Error";
        }

        public NetworkListLocalizer(IWalletLocalizationService localizationService) 
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.NetworkList] = "Chains",
                [Keys.SelectNetwork] = "Select Chain",
                [Keys.SearchNetworks] = "Search chains...",
                [Keys.ShowTestnets] = "Show testnets",
                [Keys.RefreshNetworks] = "Refresh",
                [Keys.LoadingNetworks] = "Loading chains...",
                [Keys.NoNetworksFound] = "No chains found",
                [Keys.NetworkDetails] = "Chain Details",
                [Keys.Back] = "Back",
                [Keys.SelectButton] = "Select",
                [Keys.Details] = "Details",
                [Keys.AddCustomNetwork] = "Add Custom Chain",
                [Keys.ChainlistResultsTitle] = "Chainlist suggestions",
                [Keys.ChainlistResultsDescription] = "No matching chains were found locally. Try adding one from Chainlist.",
                [Keys.ChainlistSearching] = "Searching Chainlist...",
                [Keys.ChainlistNoResults] = "No matching chains were found on Chainlist.",
                [Keys.ChainlistAddButton] = "Add chain",
                [Keys.ChainlistAddError] = "Failed to add Chainlist chain: {0}",
                [Keys.ChainlistError] = "Unable to load Chainlist results: {0}",
                [Keys.ChainlistNoRpcEndpoints] = "No RPC endpoints provided",
                [Keys.ChainlistRpcCountLabel] = "RPC endpoints: {0}",
                [Keys.AdjustSearchOrFilters] = "Try adjusting your search or filter settings",
                [Keys.ChainlistSupplementalDescription] = "We also found additional matches on Chainlist.",
                [Keys.ChainlistAlreadyAdded] = "Already added",
                [Keys.InternalResultsTitle] = "Wallet suggestions",
                [Keys.InternalResultsDescription] = "Chains provided by this wallet that are not enabled yet.",
                [Keys.InternalAddButton] = "Enable chain",
                [Keys.Error] = "Error"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.NetworkList] = "Cadenas",
                [Keys.SelectNetwork] = "Seleccionar Cadena",
                [Keys.SearchNetworks] = "Buscar cadenas...",
                [Keys.ShowTestnets] = "Mostrar cadenas de prueba",
                [Keys.RefreshNetworks] = "Actualizar",
                [Keys.LoadingNetworks] = "Cargando cadenas...",
                [Keys.NoNetworksFound] = "No se encontraron cadenas",
                [Keys.NetworkDetails] = "Detalles de Cadena",
                [Keys.Back] = "Atrás",
                [Keys.SelectButton] = "Seleccionar",
                [Keys.Details] = "Detalles",
                [Keys.AddCustomNetwork] = "Agregar Cadena Personalizada",
                [Keys.ChainlistResultsTitle] = "Sugerencias de Chainlist",
                [Keys.ChainlistResultsDescription] = "No se encontraron cadenas locales. Agrega una desde Chainlist.",
                [Keys.ChainlistSearching] = "Buscando en Chainlist...",
                [Keys.ChainlistNoResults] = "No se encontraron cadenas coincidentes en Chainlist.",
                [Keys.ChainlistAddButton] = "Agregar cadena",
                [Keys.ChainlistAddError] = "No se pudo agregar la cadena de Chainlist: {0}",
                [Keys.ChainlistError] = "No se pudieron cargar los resultados de Chainlist: {0}",
                [Keys.ChainlistNoRpcEndpoints] = "Sin endpoints RPC disponibles",
                [Keys.ChainlistRpcCountLabel] = "Endpoints RPC: {0}",
                [Keys.AdjustSearchOrFilters] = "Prueba ajustando tu búsqueda o filtros",
                [Keys.ChainlistSupplementalDescription] = "También encontramos coincidencias adicionales en Chainlist.",
                [Keys.ChainlistAlreadyAdded] = "Ya agregado",
                [Keys.InternalResultsTitle] = "Sugerencias internas",
                [Keys.InternalResultsDescription] = "Cadenas incluidas en esta cartera que aún no has habilitado.",
                [Keys.InternalAddButton] = "Habilitar cadena",
                [Keys.Error] = "Error"
            });
        }
    }
}
