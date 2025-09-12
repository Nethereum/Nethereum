using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nethereum.Wallet.UI.Components.Services
{
    public partial class PromptOverlayService : ObservableObject, IPromptOverlayService
    {
        private readonly IPromptQueueService _queueService;
        
        [ObservableProperty] private bool _isOverlayVisible;
        [ObservableProperty] private PromptRequest? _currentPrompt;
        [ObservableProperty] private int _currentIndex;
        
        public event EventHandler<OverlayStateChangedEventArgs>? OverlayStateChanged;
        
        public PromptOverlayService(IPromptQueueService queueService)
        {
            _queueService = queueService;
        }
        
        public async Task ShowPromptAsync(PromptRequest prompt)
        {
            CurrentPrompt = prompt;
            var prompts = _queueService.PendingPrompts;
            CurrentIndex = prompts.ToList().IndexOf(prompt);
            
            if (CurrentPrompt != null)
            {
                CurrentPrompt.Status = PromptStatus.InProgress;
                IsOverlayVisible = true;
                
                OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs
                {
                    IsVisible = true,
                    CurrentPrompt = CurrentPrompt
                });
            }
        }
        
        public async Task ShowPromptByIdAsync(string promptId)
        {
            var prompt = _queueService.GetPromptById(promptId);
            if (prompt != null)
            {
                await ShowPromptAsync(prompt);
            }
        }
        
        public async Task ShowNextPromptAsync()
        {
            var prompts = _queueService.PendingPrompts;
            if (prompts.Count > 0)
            {
                if (CurrentPrompt == null)
                {
                    await ShowPromptAsync(prompts[0]);
                }
                else if (CurrentIndex < prompts.Count - 1)
                {
                    await ShowPromptAsync(prompts[CurrentIndex + 1]);
                }
            }
        }
        
        public async Task ShowPreviousPromptAsync()
        {
            var prompts = _queueService.PendingPrompts;
            if (CurrentIndex > 0 && CurrentIndex < prompts.Count)
            {
                await ShowPromptAsync(prompts[CurrentIndex - 1]);
            }
        }
        
        public void HideOverlay()
        {
            IsOverlayVisible = false;
            CurrentPrompt = null;
            
            OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs
            {
                IsVisible = false,
                CurrentPrompt = null
            });
        }
        
        public void MinimizeOverlay()
        {
            IsOverlayVisible = false;
            
            OverlayStateChanged?.Invoke(this, new OverlayStateChangedEventArgs
            {
                IsVisible = false,
                CurrentPrompt = CurrentPrompt
            });
        }
    }
}