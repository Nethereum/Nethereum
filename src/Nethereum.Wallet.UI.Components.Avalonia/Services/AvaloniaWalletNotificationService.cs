using Nethereum.Wallet.UI.Components.Abstractions;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.Services
{
    public class AvaloniaWalletNotificationService : IWalletNotificationService
    {
        public void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info)
        {
            Console.WriteLine($"[{severity}] {message}");
        }

        public void ShowSuccess(string message)
        {
            Console.WriteLine($"[SUCCESS] {message}");
        }

        public void ShowError(string message)
        {
            Console.WriteLine($"[ERROR] {message}");
        }

        public void ShowWarning(string message)
        {
            Console.WriteLine($"[WARNING] {message}");
        }

        public void ShowInfo(string message)
        {
            Console.WriteLine($"[INFO] {message}");
        }

        public void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action)
        {
            Console.WriteLine($"[{severity}] {message} (Action: {action.Label})");
        }

        public void ShowSuccessWithAction(string message, NotificationAction action)
        {
            Console.WriteLine($"[SUCCESS] {message} (Action: {action.Label})");
        }

        public void ShowInfoWithAction(string message, NotificationAction action)
        {
            Console.WriteLine($"[INFO] {message} (Action: {action.Label})");
        }
    }
}