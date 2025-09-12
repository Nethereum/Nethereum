namespace Nethereum.Wallet.UI.Components.Blazor.Shared
{
    public enum WalletCardVariant
    {
        Standard,
        Compact,
        Featured,
        Minimal
    }
    public enum WalletPopupSize
    {
        Auto,
        Small,
        Medium,
        Large,
        ExtraLarge,
        FullScreen
    }
    public enum WalletPopupPosition
    {
        Center,
        Top,
        Bottom,
        TopCenter,
        BottomCenter,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
    public enum WalletPromptType
    {
        Info,
        Warning,
        Error,
        Success,
        Question,
        Input,
        Choice,
        Confirm
    }
    public enum WalletInputType
    {
        Text,
        Password,
        Email,
        Number,
        TextArea,
        MultiLine,
        Select
    }
    public enum WalletPromptResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
        Retry,
        Abort
    }
    public enum WalletEmptyStateSize
    {
        Small,
        Medium,
        Large
    }
    public enum WalletChipVariant
    {
        Default,
        Outlined,
        Filled,
        Text,
        Status,     // Status indicator (rounded, bold)
        Balance     // Balance display (monospace)
    }
    public enum WalletLoadingSize
    {
        Small,
        Medium,
        Large,
        ExtraLarge
    }
    public class WalletChoiceOption
    {
        public string Text { get; set; } = "";
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public bool IsDefault { get; set; }
        public bool IsDisabled { get; set; }
    }
    public class WalletPromptResultData
    {
        public bool IsConfirmed { get; set; }
        public string? InputValue { get; set; }
        public string? SelectedChoice { get; set; }
    }
    public class WalletFormStep
    {
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "";
        public string? LocalizationKey { get; set; }
        public Type? LocalizationComponentType { get; set; }
    }
}