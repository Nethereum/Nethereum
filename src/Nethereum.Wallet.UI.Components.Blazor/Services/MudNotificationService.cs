using MudBlazor;
using Nethereum.Wallet.UI.Components.Abstractions;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class MudNotificationService : IWalletNotificationService
    {
        private readonly ISnackbar _snackbar;

        public MudNotificationService(ISnackbar snackbar)
        {
            _snackbar = snackbar;
        }

        public void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info)
        {
            var mudSeverity = ConvertSeverity(severity);
            var icon = GetIcon(severity);
            
            _snackbar.Add(
                message, 
                mudSeverity, 
                config =>
                {
                    config.Icon = icon;
                    config.IconSize = Size.Medium;
                    config.SnackbarVariant = Variant.Filled;
                    config.VisibleStateDuration = GetDuration(severity);
                    config.HideTransitionDuration = 300;
                    config.ShowTransitionDuration = 300;
                    config.RequireInteraction = severity == NotificationSeverity.Error;
                    config.CloseAfterNavigation = true;
                });
        }

        public void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action)
        {
            var mudSeverity = ConvertSeverity(severity);
            var icon = GetIcon(severity);
            
            _snackbar.Add(
                message, 
                mudSeverity, 
                config =>
                {
                    config.Icon = icon;
                    config.IconSize = Size.Medium;
                    config.SnackbarVariant = Variant.Filled;
                    config.VisibleStateDuration = GetDuration(severity);
                    config.HideTransitionDuration = 300;
                    config.ShowTransitionDuration = 300;
                    config.RequireInteraction = severity == NotificationSeverity.Error;
                    config.CloseAfterNavigation = true;
                    
                    if (action != null)
                    {
                        config.Action = action.Label;
                        config.ActionColor = Color.Inherit;
                        // Note: MudBlazor Snackbar doesn't support custom action click handlers
                        // The action button will dismiss the snackbar by default
                        // Users can copy the transaction hash from the message text
                    }
                });
        }

        public void ShowSuccess(string message) => ShowNotification(message, NotificationSeverity.Success);
        public void ShowError(string message) => ShowNotification(message, NotificationSeverity.Error);
        public void ShowWarning(string message) => ShowNotification(message, NotificationSeverity.Warning);
        public void ShowInfo(string message) => ShowNotification(message, NotificationSeverity.Info);
        
        public void ShowSuccessWithAction(string message, NotificationAction action) 
            => ShowNotificationWithAction(message, NotificationSeverity.Success, action);
            
        public void ShowInfoWithAction(string message, NotificationAction action) 
            => ShowNotificationWithAction(message, NotificationSeverity.Info, action);

        private static MudBlazor.Severity ConvertSeverity(NotificationSeverity severity) => severity switch
        {
            NotificationSeverity.Success => MudBlazor.Severity.Success,
            NotificationSeverity.Warning => MudBlazor.Severity.Warning,
            NotificationSeverity.Error => MudBlazor.Severity.Error,
            _ => MudBlazor.Severity.Info
        };

        private static string GetIcon(NotificationSeverity severity) => severity switch
        {
            NotificationSeverity.Success => Icons.Material.Filled.CheckCircle,
            NotificationSeverity.Warning => Icons.Material.Filled.Warning,
            NotificationSeverity.Error => Icons.Material.Filled.Error,
            _ => Icons.Material.Filled.Info
        };

        private static int GetDuration(NotificationSeverity severity) => severity switch
        {
            NotificationSeverity.Success => 4000,
            NotificationSeverity.Error => 8000,
            NotificationSeverity.Warning => 6000,
            _ => 5000
        };
    }
}