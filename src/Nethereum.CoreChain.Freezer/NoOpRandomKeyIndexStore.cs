namespace Nethereum.CoreChain.Freezer
{
    public sealed class NoOpRandomKeyIndexStore : IRandomKeyIndexStore
    {
        public bool TryGetBlockNumberByHash(byte[] blockHash, out long number)
        {
            number = 0;
            return false;
        }

        public bool TryGetTxLocation(byte[] txHash, out long blockNumber, out int txIndex)
        {
            blockNumber = 0;
            txIndex = 0;
            return false;
        }

        public void PutBlockHash(byte[] hash, long number) { }

        public void PutTxLocation(byte[] txHash, long blockNumber, int txIndex) { }

        public void RemoveBlock(long number) { }
    }
}
