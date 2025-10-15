using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Blazor.Prompts
{
    public sealed class DAppChainSwitchPromptLocalizer : ComponentLocalizerBase<DAppChainSwitchPromptView>
    {
        public DAppChainSwitchPromptLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        public static class Keys
        {
            public const string Title = "Title";
            public const string SubtitleFrom = "SubtitleFrom";
            public const string SubtitleGeneric = "SubtitleGeneric";
            public const string TargetSection = "TargetSection";
            public const string CurrentSection = "CurrentSection";
            public const string ChainId = "ChainId";
            public const string Name = "Name";
            public const string Currency = "Currency";
            public const string RpcEndpoint = "RpcEndpoint";
            public const string MetadataWarning = "MetadataWarning";
            public const string CurrentUnknown = "CurrentUnknown";
            public const string CurrentChainIdTemplate = "CurrentChainIdTemplate";
            public const string AdditionInfo = "AdditionInfo";
            public const string Switch = "Switch";
            public const string Reject = "Reject";
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Switch Network",
                [Keys.SubtitleFrom] = "Request from {0}",
                [Keys.SubtitleGeneric] = "Confirm network change",
                [Keys.TargetSection] = "Target Network",
                [Keys.CurrentSection] = "Current Network",
                [Keys.ChainId] = "Chain ID",
                [Keys.Name] = "Name",
                [Keys.Currency] = "Currency",
                [Keys.RpcEndpoint] = "RPC Endpoint",
                [Keys.MetadataWarning] = "Network metadata unavailable. Approving will attempt to switch using existing configuration.",
                [Keys.CurrentUnknown] = "Unknown",
                [Keys.CurrentChainIdTemplate] = "Chain ID: {0}",
                [Keys.AdditionInfo] = "This network is not currently added to your wallet. Approving will attempt to add it automatically before switching.",
                [Keys.Switch] = "Switch",
                [Keys.Reject] = "Reject"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Cambiar de Red",
                [Keys.SubtitleFrom] = "Solicitud de {0}",
                [Keys.SubtitleGeneric] = "Confirmar cambio de red",
                [Keys.TargetSection] = "Red Destino",
                [Keys.CurrentSection] = "Red Actual",
                [Keys.ChainId] = "ID de Cadena",
                [Keys.Name] = "Nombre",
                [Keys.Currency] = "Moneda",
                [Keys.RpcEndpoint] = "Endpoint RPC",
                [Keys.MetadataWarning] = "No hay metadatos de red disponibles. Al aprobar se intentará cambiar usando la configuración existente.",
                [Keys.CurrentUnknown] = "Desconocido",
                [Keys.CurrentChainIdTemplate] = "ID de Cadena: {0}",
                [Keys.AdditionInfo] = "Esta red no está añadida actualmente a tu monedero. Al aprobar se intentará añadir automáticamente antes de cambiar.",
                [Keys.Switch] = "Cambiar",
                [Keys.Reject] = "Rechazar"
            });
        }
    }
}
