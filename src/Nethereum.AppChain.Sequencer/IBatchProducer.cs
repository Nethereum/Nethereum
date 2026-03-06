using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;

namespace Nethereum.AppChain.Sequencer
{
    public interface IBatchProducer
    {
        BigInteger LastBatchedBlock { get; }
        BigInteger NextBatchBlock { get; }

        Task<BatchProductionResult> ProduceBatchAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default);
        Task<BatchProductionResult> ProduceBatchIfDueAsync(BigInteger currentBlockNumber, CancellationToken cancellationToken = default);
        bool IsBatchDue(BigInteger blockNumber);
    }

    public class BatchProductionResult
    {
        public bool Success { get; set; }
        public BatchInfo? BatchInfo { get; set; }
        public string? FilePath { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
