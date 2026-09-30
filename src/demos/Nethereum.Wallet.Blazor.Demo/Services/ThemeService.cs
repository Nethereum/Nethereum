using MudBlazor;
using Nethereum.Wallet.Blazor.Demo.Utilities;

namespace Nethereum.Wallet.Blazor.Demo.Services;

public class ThemeService
{
    public event Action? ThemeChanged;
    
    private MudTheme _currentTheme;
    private bool _isDarkMode = false;
    private WalletBrandColor _currentBrandColor = WalletBrandColor.Professional;
    
    public MudTheme CurrentTheme => _currentTheme;
    public bool IsDarkMode => _isDarkMode;
    public WalletBrandColor CurrentBrandColor => _currentBrandColor;
    
    public static readonly (WalletBrandColor Color, string DisplayName, string Description)[] AvailableThemes = 
    [
        (WalletBrandColor.Professional, "Professional", "Clean sky blue - trustworthy and professional"),
        (WalletBrandColor.Ethereum, "Ethereum", "Ethereum purple - perfect for DeFi applications"),
        (WalletBrandColor.Bitcoin, "Bitcoin", "Bitcoin orange - recognizable crypto branding"),
        (WalletBrandColor.Forest, "Forest", "Emerald green - growth and prosperity theme"),
        (WalletBrandColor.Royal, "Royal", "Violet purple - premium and sophisticated"),
        (WalletBrandColor.Rose, "Rose", "Rose pink - modern and approachable"),
        (WalletBrandColor.Slate, "Slate", "Neutral gray - minimal and clean")
    ];
    
    public ThemeService()
    {
        _currentTheme = WalletTheme.BaseTheme;
    }
    
    public void SetDarkMode(bool isDark)
    {
        _isDarkMode = isDark;
        ThemeChanged?.Invoke();
    }
    
    public void SetThemeColor(string colorName)
    {
        var brandColor = colorName?.ToLowerInvariant() switch
        {
            "purple" => WalletBrandColor.Royal,
            "green" => WalletBrandColor.Forest,
            "orange" => WalletBrandColor.Bitcoin,
            "red" => WalletBrandColor.Rose,
            "ethereum" => WalletBrandColor.Ethereum,
            "bitcoin" => WalletBrandColor.Bitcoin,
            "slate" => WalletBrandColor.Slate,
            _ => WalletBrandColor.Professional
        };
        
        SetBrandColor(brandColor);
    }
    
    public void SetBrandColor(WalletBrandColor brandColor)
    {
        _currentBrandColor = brandColor;
        _currentTheme = WalletTheme.CreateBrandTheme(brandColor);
        ThemeChanged?.Invoke();
    }
    
    public void ResetToDefault()
    {
        _currentBrandColor = WalletBrandColor.Professional;
        _isDarkMode = false;
        _currentTheme = WalletTheme.BaseTheme;
        ThemeChanged?.Invoke();
    }
    
    public (string primary, string secondary) GetThemePreview(WalletBrandColor brandColor)
    {
        var theme = WalletTheme.CreateBrandTheme(brandColor);
        return (theme.PaletteLight.Primary.ToString(), theme.PaletteLight.Secondary.ToString());
    }
}