using Nethereum.Documentation;
namespace Nethereum.EVM
{
    public interface IChainActivations
    {
        HardforkName ResolveAt(long blockNumber, ulong timestamp);
    }

    [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "Activations that can state blob parameters for a block")]
    public interface IChainBlobSchedule
    {
        BlobParameters? BlobParametersAt(long blockNumber, ulong timestamp);
    }

    public static class ChainActivationsBlobScheduleExtensions
    {
        public static BlobParameters? BlobParametersAt(this IChainActivations activations, long blockNumber, ulong timestamp) =>
            (activations as IChainBlobSchedule)?.BlobParametersAt(blockNumber, timestamp);
    }
}
