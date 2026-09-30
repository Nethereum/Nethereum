using Nethereum.Documentation;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "IRandomKeyIndexStore — by-hash reverse lookups beside the freezer")]
    public interface IRandomKeyIndexStore
    {
        bool TryGetBlockNumberByHash(byte[] blockHash, out long number);

        bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex);

        void PutBlockHash(byte[] hash, long number);

        void PutTxLocation(byte[] txHash, long blockNumber, int txIndex);

        void RemoveBlock(long number);
    }
}
