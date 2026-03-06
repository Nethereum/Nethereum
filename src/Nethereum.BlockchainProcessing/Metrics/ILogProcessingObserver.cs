using System.Numerics;

namespace Nethereum.BlockchainProcessing.Metrics
{
    public interface ILogProcessingObserver
    {
        void OnBatchProcessed(BigInteger fromBlock, BigInteger toBlock, int logCount, double durationSeconds);
        void OnError(string reason);
        void OnReorgDetected(BigInteger rewindToBlock, BigInteger lastCanonicalBlock);
        void OnBlockProgressUpdated(BigInteger lastBlock);
        void OnGetLogsRetry(int retryNumber);
        void SetChainHead(BigInteger blockNumber);
    }
}
