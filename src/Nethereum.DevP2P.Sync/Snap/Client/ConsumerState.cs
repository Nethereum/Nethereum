namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class ConsumerState
    {
        public ulong ReqId { get; set; }
        public long FetchFailures { get; set; }
        public int SameStageFailures { get; set; }
    }
}
