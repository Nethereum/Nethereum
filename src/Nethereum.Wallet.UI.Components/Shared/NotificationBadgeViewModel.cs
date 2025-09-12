using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.Dashboard.Services;
using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Shared
{
    public partial class NotificationBadgeViewModel : ObservableObject, IDisposable
    {
        private readonly IPromptQueueService _queueService;
        private readonly IDashboardNavigationService _navigationService;
        
        [ObservableProperty] private bool _hasNotifications;
        [ObservableProperty] private int _notificationCount;
        
        public NotificationBadgeViewModel(
            IPromptQueueService queueService,
            IDashboardNavigationService navigationService)
        {
            _queueService = queueService;
            _navigationService = navigationService;
        }
        
        public void Initialize()
        {
            _queueService.QueueChanged += OnQueueChanged;
            UpdateNotificationState();
        }
        
        private void OnQueueChanged(object? sender, PromptQueueChangedEventArgs e)
        {
            UpdateNotificationState();
        }
        
        private void UpdateNotificationState()
        {
            HasNotifications = _queueService.HasPendingPrompts;
            NotificationCount = _queueService.PendingCount;
        }
        
        [RelayCommand]
        private async Task NavigateToPromptsAsync()
        {
            await _navigationService.NavigateToPluginAsync("Prompts");
        }
        
        public void Dispose()
        {
            if (_queueService != null)
            {
                _queueService.QueueChanged -= OnQueueChanged;
            }
        }
    }
}