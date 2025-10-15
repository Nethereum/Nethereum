namespace Nethereum.Wallet.UI
{
    public sealed class TypedDataSignPromptContext
    {
        public string Address { get; init; } = string.Empty;
        public string TypedDataJson { get; init; } = string.Empty;
        public string? Origin { get; init; }
        public string? DappName { get; init; }
        public string? DappIcon { get; init; }
        public string? DomainName { get; init; }
        public string? DomainVersion { get; init; }
        public string? VerifyingContract { get; init; }
        public string? PrimaryType { get; init; }
        public string? ChainId { get; init; }
    }
}
