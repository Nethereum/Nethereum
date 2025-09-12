using Nethereum.Wallet.UI.Components.NethereumWallet;

namespace Nethereum.Wallet.UI.Components.Configuration
{
    public enum DrawerBehavior
    {
        AlwaysShow,
        Responsive,
        AlwaysHidden
    }
    public interface INethereumWalletUIConfiguration
    {
        string ApplicationName { get; set; }
        string LogoPath { get; set; }
        string WelcomeLogoPath { get; set; }
        bool ShowLogo { get; set; }
        bool ShowApplicationName { get; set; }
        bool ShowNetworkInHeader { get; set; }
        bool ShowAccountDetailsInHeader { get; set; }
        DrawerBehavior DrawerBehavior { get; set; }
        int ResponsiveBreakpoint { get; set; }
        int SidebarWidth { get; set; }
        NethereumWalletConfiguration WalletConfig { get; set; }
    }
    public class NethereumWalletUIConfiguration : INethereumWalletUIConfiguration
    {
        public string ApplicationName { get; set; } = "Nethereum Wallet";
        public string LogoPath { get; set; } = "/nethereum-logo.png";
        public string WelcomeLogoPath { get; set; } = "/nethereum-logo-large.png";
        public bool ShowLogo { get; set; } = true;
        public bool ShowApplicationName { get; set; } = true;
        public bool ShowNetworkInHeader { get; set; } = true;
        public bool ShowAccountDetailsInHeader { get; set; } = true;
        public DrawerBehavior DrawerBehavior { get; set; } = DrawerBehavior.Responsive;
        public int ResponsiveBreakpoint { get; set; } = 1000;
        public int SidebarWidth { get; set; } = 200;
        public NethereumWalletConfiguration WalletConfig { get; set; } = new();
    }
}