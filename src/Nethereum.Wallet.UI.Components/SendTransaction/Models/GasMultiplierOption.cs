namespace Nethereum.Wallet.UI.Components.SendTransaction.Models
{
    public class GasMultiplierOption
    {
        public decimal Multiplier { get; set; }
        public string DisplayText { get; set; } = "";
        public string LocalizationKey { get; set; } = "";
        public string DescriptionKey { get; set; } = "";
        public bool IsRecommended { get; set; }

        public static readonly GasMultiplierOption Economy = new()
        {
            Multiplier = 0.8m,
            DisplayText = "0.8x",
            LocalizationKey = "Multiplier08",
            DescriptionKey = "Multiplier08Description"
        };

        public static readonly GasMultiplierOption Standard = new()
        {
            Multiplier = 1.0m,
            DisplayText = "1.0x",
            LocalizationKey = "Multiplier10",
            DescriptionKey = "Multiplier10Description",
            IsRecommended = true
        };

        public static readonly GasMultiplierOption Priority = new()
        {
            Multiplier = 1.2m,
            DisplayText = "1.2x",
            LocalizationKey = "Multiplier12",
            DescriptionKey = "Multiplier12Description"
        };

        public static readonly GasMultiplierOption[] All = { Economy, Standard, Priority };
    }
}