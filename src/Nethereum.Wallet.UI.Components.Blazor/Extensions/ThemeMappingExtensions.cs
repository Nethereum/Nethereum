using MudBlazor;

namespace Nethereum.Wallet.UI.Components.Blazor.Extensions
{
    public static class ThemeMappingExtensions
    {
        public static Color ToMudColor(this string colorTheme)
        {
            return colorTheme?.ToLowerInvariant() switch
            {
                "primary" => Color.Primary,
                "secondary" => Color.Secondary,
                "tertiary" => Color.Tertiary,
                "info" => Color.Info,
                "success" => Color.Success,
                "warning" => Color.Warning,
                "error" => Color.Error,
                "dark" => Color.Dark,
                "default" => Color.Default,
                "inherit" => Color.Inherit,
                "surface" => Color.Surface,
                "transparent" => Color.Transparent,
                _ => Color.Primary
            };
        }
        public static string ToMudIcon(this string iconIdentifier)
        {
            return iconIdentifier?.ToLowerInvariant() switch
            {
                "add" => Icons.Material.Filled.Add,
                "close" => Icons.Material.Filled.Close,
                "edit" => Icons.Material.Filled.Edit,
                "delete" => Icons.Material.Filled.Delete,
                "save" => Icons.Material.Filled.Save,
                "cancel" => Icons.Material.Filled.Cancel,
                "check" => Icons.Material.Filled.Check,
                "arrow_back" => Icons.Material.Filled.ArrowBack,
                "arrow_forward" => Icons.Material.Filled.ArrowForward,
                "more_vert" => Icons.Material.Filled.MoreVert,
                "copy" => Icons.Material.Filled.ContentCopy,
                "refresh" => Icons.Material.Filled.Refresh,
                "settings" => Icons.Material.Filled.Settings,
                "search" => Icons.Material.Filled.Search,
                "filter" => Icons.Material.Filled.FilterList,
                "help" => Icons.Material.Filled.HelpOutline,
                
                "account_tree" => Icons.Material.Filled.AccountTree,
                "vpn_key" => Icons.Material.Filled.VpnKey,
                "key" => Icons.Material.Filled.Key,
                "visibility" => Icons.Material.Filled.Visibility,
                "smart_toy" => Icons.Material.Filled.SmartToy,
                "account_circle" => Icons.Material.Filled.AccountCircle,
                
                _ => iconIdentifier?.Contains("Icons.") == true ? iconIdentifier : Icons.Material.Filled.AccountCircle
            };
        }
    }
}