using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainProcessing.Services
{
    /// <summary>
    /// Produces the flattened internal-transaction list for a single transaction hash.
    /// Implementations encapsulate how the trace is obtained (geth debug_traceTransaction,
    /// local EVM replay, custom tracing, etc.). The consumer (typically
    /// <see cref="InternalTransactionOrchestrator"/>) only needs a hash → list mapping.
    /// </summary>
    public interface IInternalTransactionSource
    {
        Task<List<InternalTransaction>> ProduceAsync(string transactionHash);
    }
}
