namespace Nethereum.CoreChain.Storage
{
    public interface IBulkDurabilityBoundary
    {
        void CheckpointBulk();

        void StartBackgroundFreezeIndexing();

        void FinishBulkIndexing(System.Threading.CancellationToken ct = default);
    }
}
