namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public interface IAccountTypeMetadata
    {
        string TypeName { get; }
        string DisplayName { get; }
        string Description { get; }
        string Icon { get; }
        string ColorTheme { get; }
        int SortOrder { get; }
        bool IsVisible { get; }
    }
}