using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.UI.Components.Dashboard;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public partial class PromptsPluginViewModel : ObservableObject, IDashboardPluginViewModel, IDisposable
    {
        private readonly IPromptQueueService _queueService;
        private readonly IComponentLocalizer<PromptsPluginViewModel> _localizer;
        
        public string PluginId => "Prompts";
        public string DisplayName => _localizer.GetString(PromptsPluginLocalizer.Keys.PluginName);
        public string Description => _localizer.GetString(PromptsPluginLocalizer.Keys.PluginDescription);
        public string Icon => "Notifications";
        public int SortOrder => -1;
        public bool IsVisible => _queueService.HasPendingPrompts;
        public bool IsEnabled => true;
        
        public bool IsAvailable() => true;
        
        [ObservableProperty] private PromptRequest? _currentPrompt;
        [ObservableProperty] private int _currentIndex;
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private bool _isProcessing;
        [ObservableProperty] private string _queueStatusText = "";
        [ObservableProperty] private bool _isEmpty;
        
        public bool HasPrompts => _queueService.HasPendingPrompts;
        
        public PromptsPluginViewModel(
            IPromptQueueService queueService,
            IComponentLocalizer<PromptsPluginViewModel> localizer)
        {
            _queueService = queueService;
            _localizer = localizer;
            
            _queueService.QueueChanged += OnQueueChanged;
            LoadNextPrompt();
        }
        
        private void LoadNextPrompt()
        {
            CurrentPrompt = _queueService.GetNextPrompt();
            if (CurrentPrompt != null)
            {
                var pendingList = _queueService.PendingPrompts.ToList();
                CurrentIndex = pendingList.IndexOf(CurrentPrompt) + 1;
                TotalCount = _queueService.PendingCount;
                CurrentPrompt.Status = PromptStatus.InProgress;
                IsEmpty = false;
            }
            else
            {
                CurrentIndex = 0;
                TotalCount = 0;
                IsEmpty = true;
            }
            UpdateQueueStatus();
            OnPropertyChanged(nameof(HasPrompts));
            OnPropertyChanged(nameof(IsVisible));
        }
        
        private void UpdateQueueStatus()
        {
            if (TotalCount > 1)
            {
                QueueStatusText = _localizer.GetString(
                    PromptsPluginLocalizer.Keys.QueueStatus,
                    CurrentIndex.ToString(),
                    TotalCount.ToString()
                );
            }
            else if (TotalCount == 1)
            {
                QueueStatusText = _localizer.GetString(PromptsPluginLocalizer.Keys.SinglePrompt);
            }
            else
            {
                QueueStatusText = _localizer.GetString(PromptsPluginLocalizer.Keys.NoPrompts);
            }
        }
        
        public async Task ApproveCurrentPromptAsync(object? result)
        {
            if (CurrentPrompt == null) return;
            
            IsProcessing = true;
            try
            {
                await _queueService.CompletePromptAsync(CurrentPrompt.Id, result);
                LoadNextPrompt();
            }
            finally
            {
                IsProcessing = false;
            }
        }
        
        [RelayCommand]
        private async Task RejectCurrentPromptAsync()
        {
            if (CurrentPrompt == null) return;
            
            await _queueService.RejectPromptAsync(
                CurrentPrompt.Id, 
                _localizer.GetString(PromptsPluginLocalizer.Keys.UserRejected)
            );
            LoadNextPrompt();
        }
        
        [RelayCommand]
        private async Task RejectAllPromptsAsync()
        {
            await _queueService.RejectAllAsync();
            CurrentPrompt = null;
            IsEmpty = true;
            UpdateQueueStatus();
        }
        
        private void OnQueueChanged(object? sender, PromptQueueChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasPrompts));
            OnPropertyChanged(nameof(IsVisible));
            if (CurrentPrompt == null || CurrentPrompt.Status != PromptStatus.InProgress)
            {
                LoadNextPrompt();
            }
            else
            {
                TotalCount = _queueService.PendingCount;
                UpdateQueueStatus();
            }
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
