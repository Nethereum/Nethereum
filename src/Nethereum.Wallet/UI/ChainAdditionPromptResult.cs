using System.Numerics;

namespace Nethereum.Wallet.UI
{
    public sealed class ChainAdditionPromptResult
    {
        public bool Approved { get; init; }
        public BigInteger? ChainId { get; init; }
        public bool SwitchRequested { get; init; }
        public bool SwitchSucceeded { get; init; }
        public string? ErrorMessage { get; init; }

        public static ChainAdditionPromptResult Rejected(string? error = null) => new()
        {
            Approved = false,
            ErrorMessage = error,
            SwitchRequested = false,
            SwitchSucceeded = false
        };

        public static ChainAdditionPromptResult ApprovedResult(BigInteger? chainId, bool switchRequested, bool switchSucceeded) => new()
        {
            Approved = true,
            ChainId = chainId,
            SwitchRequested = switchRequested,
            SwitchSucceeded = switchSucceeded
        };
    }
}
