using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Sync;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Strategy for what a follower does with each synced block at execution time.
    /// Implementations:
    /// - FullReExecutionStrategy: replay each tx, validate stateRoot (full archive)
    /// - HeaderOnlyExecutionStrategy: skip execution, header chain only (light follower / indexer)
    /// - WitnessVerifyExecutionStrategy: verify state proof without re-executing (future, stateless)
    /// </summary>
    public interface IExecutionStrategy
    {
        Task<ExecutionStrategyResult> ExecuteAsync(LiveBlockData block, CancellationToken cancellationToken = default);
    }

    public class ExecutionStrategyResult
    {
        public bool Success { get; set; }
        public byte[]? ComputedStateRoot { get; set; }
        public bool StateRootValidated { get; set; }
        public IList<Receipt> Receipts { get; set; } = new List<Receipt>();
        public string? ErrorMessage { get; set; }

        public static ExecutionStrategyResult Skipped() => new() { Success = true };
        public static ExecutionStrategyResult Failure(string error) => new() { Success = false, ErrorMessage = error };
    }
}
