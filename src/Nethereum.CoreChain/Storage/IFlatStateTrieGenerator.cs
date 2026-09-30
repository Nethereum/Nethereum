using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public interface IFlatStateTrieGenerator
    {
        void PruneFlatStateBeyond(IReadOnlyList<SnapSyncAccountTask> durableTasks);

        Task<FlatTrieGenerationResult> GenerateTrieFromFlatAsync(
            byte[] expectedRoot, Action<string> progress, CancellationToken ct);
    }

    public sealed record FlatTrieGenerationResult(
        byte[] Root,
        long AccountsScanned,
        long SlotsScanned,
        long AccountRootsRewritten,
        long DanglingSlotsDeleted);
}
