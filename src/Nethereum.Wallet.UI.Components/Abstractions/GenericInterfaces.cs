
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Abstractions
{
    public enum NotificationSeverity
    {
        Success,
        Info,
        Warning,
        Error
    }

    public class NotificationAction
    {
        public string Label { get; set; } = "View";
        public Action? OnClick { get; set; }
    }

    public interface IWalletNotificationService
    {
        void ShowNotification(string message, NotificationSeverity severity = NotificationSeverity.Info);
        void ShowSuccess(string message);
        void ShowError(string message);
        void ShowWarning(string message);
        void ShowInfo(string message);
        
        void ShowNotificationWithAction(string message, NotificationSeverity severity, NotificationAction action);
        void ShowSuccessWithAction(string message, NotificationAction action);
        void ShowInfoWithAction(string message, NotificationAction action);
    }

    public interface IWalletDialogService
    {
        Task<bool> ShowConfirmationAsync(string title, string message);
        Task ShowMessageAsync(string title, string message);
        Task<T?> ShowDialogAsync<T>(object? parameters = null) where T : class;
        Task<bool> ShowWarningConfirmationAsync(string title, string message, string confirmText = "Remove", string cancelText = "Cancel");
        Task ShowErrorAsync(string title, string message);
        Task ShowSuccessAsync(string title, string message);
    }

    public interface IWalletNavigationService
    {
        Task GoToAsync(string route);
    }

    public interface IWalletLoadingService
    {
        bool IsLoading { get; }
        string? LoadingMessage { get; }
        double Progress { get; }
        
        void SetLoading(bool isLoading, string? message = null);
        void ShowProgress(double percentage, string? message = null);
    }
}
