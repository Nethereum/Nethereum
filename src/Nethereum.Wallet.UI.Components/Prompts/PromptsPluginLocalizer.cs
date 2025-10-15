using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public class PromptsPluginLocalizer : ComponentLocalizerBase<PromptsPluginViewModel>
    {
        public PromptsPluginLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        public static class Keys
        {
            public const string PluginName = "PluginName";
            public const string PluginDescription = "PluginDescription";
            public const string EmptyTitle = "EmptyTitle";
            public const string EmptyDescription = "EmptyDescription";
            public const string QueueStatus = "QueueStatus";
            public const string SinglePrompt = "SinglePrompt";
            public const string NoPrompts = "NoPrompts";
            public const string RejectAll = "RejectAll";
            public const string Reject = "Reject";
            public const string UserRejected = "UserRejected";
            public const string UnknownPromptType = "UnknownPromptType";
            public const string ProcessingRequest = "ProcessingRequest";
            public const string RequestFrom = "RequestFrom";
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.PluginName] = "Notifications",
                [Keys.PluginDescription] = "View and manage pending transaction and signature requests",
                [Keys.EmptyTitle] = "No Pending Requests",
                [Keys.EmptyDescription] = "You have no pending transaction or signature requests",
                [Keys.QueueStatus] = "Request {0} of {1}",
                [Keys.SinglePrompt] = "1 pending request",
                [Keys.NoPrompts] = "No pending requests",
                [Keys.RejectAll] = "Reject All",
                [Keys.Reject] = "Reject",
                [Keys.UserRejected] = "User rejected the request",
                [Keys.UnknownPromptType] = "Unknown request type",
                [Keys.ProcessingRequest] = "Processing request...",
                [Keys.RequestFrom] = "Request from {0}"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.PluginName] = "Notificaciones",
                [Keys.PluginDescription] = "Ver y gestionar solicitudes pendientes de transacción y firma",
                [Keys.EmptyTitle] = "Sin Solicitudes Pendientes",
                [Keys.EmptyDescription] = "No tienes solicitudes pendientes de transacción o firma",
                [Keys.QueueStatus] = "Solicitud {0} de {1}",
                [Keys.SinglePrompt] = "1 solicitud pendiente",
                [Keys.NoPrompts] = "Sin solicitudes pendientes",
                [Keys.RejectAll] = "Rechazar Todas",
                [Keys.Reject] = "Rechazar",
                [Keys.UserRejected] = "El usuario rechazó la solicitud",
                [Keys.UnknownPromptType] = "Tipo de solicitud desconocido",
                [Keys.ProcessingRequest] = "Procesando solicitud...",
                [Keys.RequestFrom] = "Solicitud de {0}"
            });
        }
    }
}
