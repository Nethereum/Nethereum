using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Shared
{
    public class NotificationBadgeLocalizer : ComponentLocalizerBase<NotificationBadgeViewModel>
    {
        public NotificationBadgeLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        public static class Keys
        {
            public const string NotificationTitle = "NotificationTitle";
            public const string SingleNotification = "SingleNotification";
            public const string MultipleNotifications = "MultipleNotifications";
            public const string NoNotifications = "NoNotifications";
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.NotificationTitle] = "Pending Requests",
                [Keys.SingleNotification] = "1 pending request",
                [Keys.MultipleNotifications] = "{0} pending requests",
                [Keys.NoNotifications] = "No notifications"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.NotificationTitle] = "Solicitudes Pendientes",
                [Keys.SingleNotification] = "1 solicitud pendiente",
                [Keys.MultipleNotifications] = "{0} solicitudes pendientes",
                [Keys.NoNotifications] = "Sin notificaciones"
            });
        }
    }
}