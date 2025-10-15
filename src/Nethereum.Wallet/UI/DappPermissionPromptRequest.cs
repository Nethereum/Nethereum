namespace Nethereum.Wallet.UI
{
    public sealed class DappPermissionPromptRequest
    {
        public string Origin { get; init; } = string.Empty;
        public string? DappName { get; init; }
        public string? DappIcon { get; init; }
        public string AccountAddress { get; init; } = string.Empty;
    }
}
