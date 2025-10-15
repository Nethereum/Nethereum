using System;

namespace Nethereum.Wallet.UI
{
    public sealed record DappConnectionContext
    {
        public string Origin { get; init; } = string.Empty;
        public string? Title { get; init; }
        public string? Icon { get; init; }
    }
}
