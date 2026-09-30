using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public enum FlatRecoverySource
    {
        ReplayJournal,

        ReconcileFromTrie,
    }

    public interface INodeHistoryRecoverable
    {
        Task<ulong> RecoverToAsync(
            ulong targetBlock, FlatRecoverySource flatSource, Action<string> progress, CancellationToken ct,
            bool skipVerify = false);
    }
}
