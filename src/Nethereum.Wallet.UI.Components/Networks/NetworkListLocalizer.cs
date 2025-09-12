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
        }

        public NetworkListLocalizer(IWalletLocalizationService localizationService) 
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.NetworkList] = "Networks",
                [Keys.SelectNetwork] = "Select Network", 
                [Keys.SearchNetworks] = "Search networks...",
                [Keys.ShowTestnets] = "Show testnets",
                [Keys.RefreshNetworks] = "Refresh",
                [Keys.LoadingNetworks] = "Loading networks...",
                [Keys.NoNetworksFound] = "No networks found",
                [Keys.NetworkDetails] = "Network Details",
                [Keys.Back] = "Back",
                [Keys.SelectButton] = "Select",
                [Keys.Details] = "Details",
                [Keys.AddCustomNetwork] = "Add Custom Network"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.NetworkList] = "Redes",
                [Keys.SelectNetwork] = "Seleccionar Red",
                [Keys.SearchNetworks] = "Buscar redes...",
                [Keys.ShowTestnets] = "Mostrar redes de prueba",
                [Keys.RefreshNetworks] = "Actualizar",
                [Keys.LoadingNetworks] = "Cargando redes...",
                [Keys.NoNetworksFound] = "No se encontraron redes",
                [Keys.NetworkDetails] = "Detalles de Red",
                [Keys.Back] = "Atrás",
                [Keys.SelectButton] = "Seleccionar",
                [Keys.Details] = "Detalles",
                [Keys.AddCustomNetwork] = "Agregar Red Personalizada"
            });
        }
    }
}