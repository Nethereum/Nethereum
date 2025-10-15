namespace Nethereum.Wallet.UI
{
    public sealed class SignaturePromptContext
    {
        public string Method { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? DecodedMessage { get; init; }
        public bool IsMessageHex { get; init; }
        public string Address { get; init; } = string.Empty;
        public string? Origin { get; init; }
        public string? DappName { get; init; }
        public string? DappIcon { get; init; }
    }
}
