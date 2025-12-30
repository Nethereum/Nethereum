using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Services
{
    public sealed class PromptInfrastructureLocalizer : ComponentLocalizerBase<PromptInfrastructureLocalizer>
    {
        public PromptInfrastructureLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        public static class Keys
        {
            public const string GenericRequestTimedOut = "GenericRequestTimedOut";
            public const string TransactionPromptTimedOut = "TransactionPromptTimedOut";
            public const string NetworkSwitchPromptTimedOut = "NetworkSwitchPromptTimedOut";
            public const string PermissionRequestTimedOut = "PermissionRequestTimedOut";
            public const string PermissionRequestCanceled = "PermissionRequestCanceled";
            public const string PermissionRequestFailed = "PermissionRequestFailed";
            public const string ChainAdditionInvalidRequest = "ChainAdditionInvalidRequest";
            public const string PromptNotFound = "PromptNotFound";
            public const string ChainAdditionTimedOut = "ChainAdditionTimedOut";
            public const string ChainAdditionCanceled = "ChainAdditionCanceled";
            public const string UserRejected = "UserRejected";
            public const string BulkRejection = "BulkRejection";
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.GenericRequestTimedOut] = "The request timed out.",
                [Keys.TransactionPromptTimedOut] = "The transaction request timed out.",
                [Keys.NetworkSwitchPromptTimedOut] = "The chain switch request timed out.",
                [Keys.PermissionRequestTimedOut] = "The permission request timed out.",
                [Keys.PermissionRequestCanceled] = "The permission request was canceled.",
                [Keys.PermissionRequestFailed] = "The permission request failed.",
                [Keys.ChainAdditionInvalidRequest] = "The received chain information is invalid.",
                [Keys.PromptNotFound] = "The request context could not be found.",
                [Keys.ChainAdditionTimedOut] = "The add chain request timed out.",
                [Keys.ChainAdditionCanceled] = "The add chain request was canceled.",
                [Keys.UserRejected] = "The user rejected the request.",
                [Keys.BulkRejection] = "All pending requests were rejected."
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.GenericRequestTimedOut] = "La solicitud superó el tiempo de espera.",
                [Keys.TransactionPromptTimedOut] = "La solicitud de transacción superó el tiempo de espera.",
                [Keys.NetworkSwitchPromptTimedOut] = "La solicitud de cambio de cadena superó el tiempo de espera.",
                [Keys.PermissionRequestTimedOut] = "La solicitud de permiso superó el tiempo de espera.",
                [Keys.PermissionRequestCanceled] = "La solicitud de permiso fue cancelada.",
                [Keys.PermissionRequestFailed] = "La solicitud de permiso falló.",
                [Keys.ChainAdditionInvalidRequest] = "La información de la cadena recibida no es válida.",
                [Keys.PromptNotFound] = "No se pudo encontrar el contexto de la solicitud.",
                [Keys.ChainAdditionTimedOut] = "La solicitud para agregar la cadena superó el tiempo de espera.",
                [Keys.ChainAdditionCanceled] = "La solicitud para agregar la cadena fue cancelada.",
                [Keys.UserRejected] = "El usuario rechazó la solicitud.",
                [Keys.BulkRejection] = "Se rechazaron todas las solicitudes pendientes."
            });
        }
    }
}
