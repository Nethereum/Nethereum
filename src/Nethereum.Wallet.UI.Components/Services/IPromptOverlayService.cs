using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Services
{
    public interface IPromptOverlayService
    {
        bool IsOverlayVisible { get; }
        PromptRequest? CurrentPrompt { get; }
        int CurrentIndex { get; }
        
        Task ShowPromptAsync(PromptRequest prompt);
        Task ShowPromptByIdAsync(string promptId);
        Task ShowNextPromptAsync();
        Task ShowPreviousPromptAsync();
        void HideOverlay();
        void MinimizeOverlay();
        
        event EventHandler<OverlayStateChangedEventArgs>? OverlayStateChanged;
    }
    
    public class OverlayStateChangedEventArgs : EventArgs
    {
        public bool IsVisible { get; set; }
        public PromptRequest? CurrentPrompt { get; set; }
    }
}