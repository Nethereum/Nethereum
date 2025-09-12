using Microsoft.AspNetCore.Components;

namespace Nethereum.Wallet.UI.Components.Blazor.Shared
{
    public abstract class ResponsiveComponentBase : ComponentBase
    {
        [Parameter] public bool IsCompactMode { get; set; } = false;
        [Parameter] public int ComponentWidth { get; set; } = 800;
        
        protected string GetResponsiveClasses(string baseClass)
        {
            var classes = baseClass;
            if (IsCompactMode)
            {
                classes += $" {baseClass}-compact";
            }
            return classes;
        }
        
        protected MudBlazor.Size GetResponsiveAvatarSize()
        {
            return IsCompactMode ? MudBlazor.Size.Medium : MudBlazor.Size.Large;
        }
        
        protected MudBlazor.Typo GetResponsiveTitleTypo()
        {
            return IsCompactMode ? MudBlazor.Typo.body1 : MudBlazor.Typo.subtitle1;
        }
        
        protected MudBlazor.Typo GetResponsiveBodyTypo()
        {
            return MudBlazor.Typo.body2;
        }
        
        protected string GetResponsiveAddressStyle()
        {
            var fontSize = IsCompactMode ? "0.7rem" : "0.75rem";
            return $"font-family: 'Roboto Mono', monospace; font-size: {fontSize}; color: var(--mud-palette-text-secondary);";
        }
        
        protected int GetResponsiveSpacing(int compactValue = 1, int desktopValue = 4)
        {
            return IsCompactMode ? compactValue : desktopValue;
        }
        
        protected string GetResponsivePadding()
        {
            return IsCompactMode ? "pa-2" : "pa-4";
        }
    }
}