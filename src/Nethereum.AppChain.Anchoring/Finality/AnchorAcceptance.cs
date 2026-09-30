namespace Nethereum.AppChain.Anchoring.Finality
{
    public enum AnchorAcceptance
    {
        NoAnchorYet,
        Accepted,
        NotAdvanced,
        NotYetLocal,
        Diverged
    }
}
