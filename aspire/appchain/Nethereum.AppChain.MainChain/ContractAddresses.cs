namespace Nethereum.AppChain.MainChain;

public class ContractAddresses
{
    public string? Anchor { get; set; }
    public string? Authority { get; set; }
    public ulong AppChainId { get; set; }
    public bool IsReady => !string.IsNullOrEmpty(Anchor);
}
