using MudBlazor;

namespace Nethereum.Wallet.Blazor.Demo.Utilities;

public static class WalletTheme
{
    public static readonly MudTheme BaseTheme = new()
    {
        PaletteLight = new PaletteLight()
        {
            AppbarBackground = "#ffffff",
            Background = "#f8fafc",
            Surface = "#ffffff",
            BackgroundGray = "#f1f5f9",
            DrawerBackground = "#ffffff",
            
            Primary = "#0ea5e9",
            Secondary = "#64748b",
            Tertiary = "#8b5cf6",
            
            Info = "#0284c7",
            Success = "#059669",
            Warning = "#d97706",
            Error = "#dc2626",
            
            TextPrimary = "#0f172a",
            TextSecondary = "#334155",
            TextDisabled = "#94a3b8",
            
            ActionDefault = "#64748b",
            ActionDisabled = "#94a3b8",
            ActionDisabledBackground = "#f1f5f9",
            
            Divider = "#e2e8f0",
            LinesDefault = "#d1d5db",
            LinesInputs = "#9ca3af"
        },
        
        PaletteDark = new PaletteDark()
        {
            AppbarBackground = "#0f172a",
            Background = "#020617",
            Surface = "#0f172a",
            BackgroundGray = "#1e293b",
            DrawerBackground = "#0f172a",
            
            Primary = "#38bdf8",
            Secondary = "#94a3b8",
            Tertiary = "#a78bfa",
            
            Info = "#0ea5e9",
            Success = "#10b981",
            Warning = "#f59e0b",
            Error = "#f87171",
            
            TextPrimary = "#f8fafc",
            TextSecondary = "#cbd5e1",
            TextDisabled = "#64748b",
            
            ActionDefault = "#94a3b8",
            ActionDisabled = "#64748b",
            ActionDisabledBackground = "#1e293b",
            
            Divider = "#334155",
            LinesDefault = "#475569",
            LinesInputs = "#64748b"
        },
        
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "0.75rem"
        }
    };

    public static MudTheme CreateBrandTheme(WalletBrandColor brandColor)
    {
        var (lightPrimary, darkPrimary, lightSecondary, darkSecondary) = GetBrandColors(brandColor);
        
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                AppbarBackground = BaseTheme.PaletteLight.AppbarBackground,
                Background = BaseTheme.PaletteLight.Background,
                Surface = BaseTheme.PaletteLight.Surface,
                BackgroundGray = BaseTheme.PaletteLight.BackgroundGray,
                DrawerBackground = BaseTheme.PaletteLight.DrawerBackground,
                
                Primary = lightPrimary,
                Secondary = lightSecondary,
                Tertiary = BaseTheme.PaletteLight.Tertiary,
                
                Info = BaseTheme.PaletteLight.Info,
                Success = BaseTheme.PaletteLight.Success,
                Warning = BaseTheme.PaletteLight.Warning,
                Error = BaseTheme.PaletteLight.Error,
                
                TextPrimary = BaseTheme.PaletteLight.TextPrimary,
                TextSecondary = BaseTheme.PaletteLight.TextSecondary,
                TextDisabled = BaseTheme.PaletteLight.TextDisabled,
                
                ActionDefault = BaseTheme.PaletteLight.ActionDefault,
                ActionDisabled = BaseTheme.PaletteLight.ActionDisabled,
                ActionDisabledBackground = BaseTheme.PaletteLight.ActionDisabledBackground,
                
                Divider = BaseTheme.PaletteLight.Divider,
                LinesDefault = BaseTheme.PaletteLight.LinesDefault,
                LinesInputs = BaseTheme.PaletteLight.LinesInputs
            },
            
            PaletteDark = new PaletteDark()
            {
                AppbarBackground = BaseTheme.PaletteDark.AppbarBackground,
                Background = BaseTheme.PaletteDark.Background,
                Surface = BaseTheme.PaletteDark.Surface,
                BackgroundGray = BaseTheme.PaletteDark.BackgroundGray,
                DrawerBackground = BaseTheme.PaletteDark.DrawerBackground,
                
                Primary = darkPrimary,
                Secondary = darkSecondary,
                Tertiary = BaseTheme.PaletteDark.Tertiary,
                
                Info = BaseTheme.PaletteDark.Info,
                Success = BaseTheme.PaletteDark.Success,
                Warning = BaseTheme.PaletteDark.Warning,
                Error = BaseTheme.PaletteDark.Error,
                
                TextPrimary = BaseTheme.PaletteDark.TextPrimary,
                TextSecondary = BaseTheme.PaletteDark.TextSecondary,
                TextDisabled = BaseTheme.PaletteDark.TextDisabled,
                
                ActionDefault = BaseTheme.PaletteDark.ActionDefault,
                ActionDisabled = BaseTheme.PaletteDark.ActionDisabled,
                ActionDisabledBackground = BaseTheme.PaletteDark.ActionDisabledBackground,
                
                Divider = BaseTheme.PaletteDark.Divider,
                LinesDefault = BaseTheme.PaletteDark.LinesDefault,
                LinesInputs = BaseTheme.PaletteDark.LinesInputs
            },
            
            LayoutProperties = BaseTheme.LayoutProperties
        };
    }

    private static (string lightPrimary, string darkPrimary, string lightSecondary, string darkSecondary) 
        GetBrandColors(WalletBrandColor brandColor) => brandColor switch
    {
        WalletBrandColor.Ethereum => ("#627eea", "#818cf8", "#a5b4fc", "#c7d2fe"),
        WalletBrandColor.Bitcoin => ("#f7931a", "#fb923c", "#fed7aa", "#fef3c7"),
        WalletBrandColor.Professional => ("#0ea5e9", "#38bdf8", "#7dd3fc", "#bae6fd"),
        WalletBrandColor.Forest => ("#059669", "#10b981", "#6ee7b7", "#a7f3d0"),
        WalletBrandColor.Royal => ("#7c3aed", "#8b5cf6", "#c4b5fd", "#e9d5ff"),
        WalletBrandColor.Rose => ("#e11d48", "#f43f5e", "#fda4af", "#fecdd3"),
        WalletBrandColor.Slate => ("#475569", "#64748b", "#94a3b8", "#cbd5e1"),
        _ => ("#0ea5e9", "#38bdf8", "#7dd3fc", "#bae6fd")
    };
}

public enum WalletBrandColor
{
    Professional,
    Ethereum,
    Bitcoin,
    Forest,
    Royal,
    Rose,
    Slate
}