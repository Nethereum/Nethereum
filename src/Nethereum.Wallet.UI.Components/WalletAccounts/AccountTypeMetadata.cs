namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public class AccountTypeMetadata : IAccountTypeMetadata
    {
        public string TypeName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "account_circle";
        public string ColorTheme { get; set; } = "primary";
        public int SortOrder { get; set; } = 100;
        public bool IsVisible { get; set; } = true;

        public AccountTypeMetadata() { }

        public AccountTypeMetadata(
            string typeName,
            string displayName,
            string description,
            string icon = "account_circle",
            string colorTheme = "primary",
            int sortOrder = 100,
            bool isVisible = true)
        {
            TypeName = typeName;
            DisplayName = displayName;
            Description = description;
            Icon = icon;
            ColorTheme = colorTheme;
            SortOrder = sortOrder;
            IsVisible = isVisible;
        }
    }
}