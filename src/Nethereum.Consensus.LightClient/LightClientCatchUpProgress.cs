using Nethereum.Documentation;

namespace Nethereum.Consensus.LightClient
{
    [NethereumDocExample(DocSection.Consensus, "light-client", "Catch-up stages reported while the store is behind")]
    public enum LightClientCatchUpStage
    {
        PeriodApplied,
        Completed,
        UpdateRejected,
        Rebootstrapped,
        RebootstrapFailed
    }

    [NethereumDocExample(DocSection.Consensus, "light-client", "A catch-up progress report")]
    public sealed class LightClientCatchUpProgress
    {
        public LightClientCatchUpProgress(LightClientCatchUpStage stage, ulong storePeriod, ulong targetPeriod, string reason)
        {
            Stage = stage;
            StorePeriod = storePeriod;
            TargetPeriod = targetPeriod;
            Reason = reason;
        }

        public LightClientCatchUpStage Stage { get; }
        public ulong StorePeriod { get; }
        public ulong TargetPeriod { get; }
        public string Reason { get; }
    }
}
