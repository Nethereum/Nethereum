using MudBlazor;
using Nethereum.Wallet.UI.Components.Abstractions;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{
    public class BlazorWalletNotificationService : IWalletNotificationService
    {
        private readonly ISnackbar _snackbar;

        public BlazorWalletNotificationService(ISnackbar snackbar)
        {
            _snackbar = snackbar;
        }

        public void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info)
        {
            var mudSeverity = ConvertSeverity(severity);
            _snackbar.Add(message, mudSeverity, configure: options =>
            {
                options.VisibleStateDuration = GetDurationBySeverity(severity);
                options.ShowTransitionDuration = 300;
                options.HideTransitionDuration = 200;
                options.SnackbarVariant = Variant.Filled;
                options.CloseAfterNavigation = true;
            });
        }

        public void ShowSuccess(string message)
        {
            _snackbar.Add(message, Severity.Success, configure: options =>
            {
                options.VisibleStateDuration = 3000;
                options.ShowTransitionDuration = 300;
                options.HideTransitionDuration = 200;
                options.SnackbarVariant = Variant.Filled;
                options.CloseAfterNavigation = true;
                options.Action = "✓";
                options.ActionColor = Color.Inherit;
            });
        }

        public void ShowError(string message)
        {
            _snackbar.Add(message, Severity.Error, configure: options =>
            {
                options.VisibleStateDuration = 5000;
                options.ShowTransitionDuration = 300;
                options.HideTransitionDuration = 200;
                options.SnackbarVariant = Variant.Filled;
                options.CloseAfterNavigation = true;
                options.Action = "×";
                options.ActionColor = Color.Inherit;
                options.RequireInteraction = true;
            });
        }

        public void ShowWarning(string message)
        {
            _snackbar.Add(message, Severity.Warning, configure: options =>
            {
                options.VisibleStateDuration = 4000;
                options.ShowTransitionDuration = 300;
                options.HideTransitionDuration = 200;
                options.SnackbarVariant = Variant.Filled;
                options.CloseAfterNavigation = true;
                options.Action = "⚠";
                options.ActionColor = Color.Inherit;
            });
        }

        public void ShowInfo(string message)
        {
            _snackbar.Add(message, Severity.Info, configure: options =>
            {
                options.VisibleStateDuration = 3000;
                options.ShowTransitionDuration = 300;
                options.HideTransitionDuration = 200;
                options.SnackbarVariant = Variant.Filled;
                options.CloseAfterNavigation = true;
                options.Action = "ℹ";
                options.ActionColor = Color.Inherit;
            });
        }

        public void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action)
        {
            var mudSeverity = ConvertSeverity(severity);
            _snackbar.Add(message, mudSeverity, configure: options =>
            {
                options.VisibleStateDuration = GetDurationBySeverity(severity);
                options.ShowTransitionDuration = 300;
                options.HideTransitionDuration = 200;
                options.SnackbarVariant = Variant.Filled;
                options.CloseAfterNavigation = true;
                
                if (action != null)
                {
                    options.Action = action.Label;
                    options.ActionColor = Color.Inherit;
                    // Note: MudBlazor Snackbar doesn't support custom action click handlers
                    // The action button will dismiss the snackbar by default
                    // Users can copy the transaction hash from the message text
                }
            });
        }

        public void ShowSuccessWithAction(string message, NotificationAction action) 
            => ShowNotificationWithAction(message, NotificationSeverity.Success, action);
            
        public void ShowInfoWithAction(string message, NotificationAction action) 
            => ShowNotificationWithAction(message, NotificationSeverity.Info, action);

        private static Severity ConvertSeverity(NotificationSeverity severity)
        {
            return severity switch
            {
                NotificationSeverity.Success => Severity.Success,
                NotificationSeverity.Info => Severity.Info,
                NotificationSeverity.Warning => Severity.Warning,
                NotificationSeverity.Error => Severity.Error,
                _ => Severity.Info
            };
        }

        private static int GetDurationBySeverity(NotificationSeverity severity)
        {
            return severity switch
            {
                NotificationSeverity.Success => 3000,
                NotificationSeverity.Info => 3000,
                NotificationSeverity.Warning => 4000,
                NotificationSeverity.Error => 5000,
                _ => 3000
            };
        }
    }
}