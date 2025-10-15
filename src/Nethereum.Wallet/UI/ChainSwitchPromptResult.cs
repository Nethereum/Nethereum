namespace Nethereum.Wallet.UI
{
    public sealed class ChainSwitchPromptResult
    {
        public bool Approved { get; init; }
        public bool ChainAdded { get; init; }
        public bool SwitchSucceeded { get; init; }
        public string? ErrorMessage { get; init; }

        public static ChainSwitchPromptResult Rejected(string? error = null) => new()
        {
            Approved = false,
            ChainAdded = false,
            SwitchSucceeded = false,
            ErrorMessage = error
        };

        public static ChainSwitchPromptResult Failure(string? error, bool chainAdded = false, bool switchSucceeded = false) => new()
        {
            Approved = true,
            ChainAdded = chainAdded,
            SwitchSucceeded = switchSucceeded,
            ErrorMessage = error
        };

        public static ChainSwitchPromptResult Success(bool chainAdded, bool switchSucceeded = true) => new()
        {
            Approved = true,
            ChainAdded = chainAdded,
            SwitchSucceeded = switchSucceeded,
            ErrorMessage = null
        };
    }
}
