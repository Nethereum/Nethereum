using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal static class ChunkedFetch
    {
        internal static async Task<(List<TItem> Items, IReadOnlyCollection<Guid> PeerIds)> RunAsync<TItem>(
            IReadOnlyList<byte[]> hashes, int chunkSize, int maxParallel, Func<int> activePeerCount,
            Func<IReadOnlyList<byte[]>, Task<(List<TItem> Items, Guid PeerId)>> fetch)
        {
            if (hashes.Count <= chunkSize || maxParallel <= 1)
                return await FetchWholeAsync(hashes, fetch).ConfigureAwait(false);

            var activePeers = activePeerCount();
            if (activePeers <= 1)
                return await FetchWholeAsync(hashes, fetch).ConfigureAwait(false);

            var effectiveParallel = Math.Min(maxParallel, activePeers);
            var chunks = SplitIntoChunks(hashes, chunkSize, effectiveParallel);

            var chunkTasks = new Task<(List<TItem> Items, Guid PeerId)>[chunks.Count];
            for (int i = 0; i < chunks.Count; i++) chunkTasks[i] = fetch(chunks[i]);

            var results = await Task.WhenAll(chunkTasks).ConfigureAwait(false);
            return Merge(hashes.Count, results);
        }

        internal static List<IReadOnlyList<byte[]>> SplitIntoChunks(
            IReadOnlyList<byte[]> hashes, int chunkSize, int maxChunks)
        {
            var targetChunkCount = Math.Min(maxChunks, (hashes.Count + chunkSize - 1) / chunkSize);
            if (targetChunkCount <= 1)
                return new List<IReadOnlyList<byte[]>> { hashes };

            var perChunk = (hashes.Count + targetChunkCount - 1) / targetChunkCount;
            var chunks = new List<IReadOnlyList<byte[]>>(targetChunkCount);
            for (int start = 0; start < hashes.Count; start += perChunk)
            {
                var end = Math.Min(start + perChunk, hashes.Count);
                var slice = new byte[end - start][];
                for (int j = start; j < end; j++) slice[j - start] = hashes[j];
                chunks.Add(slice);
            }
            return chunks;
        }

        private static async Task<(List<TItem> Items, IReadOnlyCollection<Guid> PeerIds)> FetchWholeAsync<TItem>(
            IReadOnlyList<byte[]> hashes,
            Func<IReadOnlyList<byte[]>, Task<(List<TItem> Items, Guid PeerId)>> fetch)
        {
            var (single, singlePeer) = await fetch(hashes).ConfigureAwait(false);
            return (single, new[] { singlePeer });
        }

        private static (List<TItem> Items, IReadOnlyCollection<Guid> PeerIds) Merge<TItem>(
            int capacity, (List<TItem> Items, Guid PeerId)[] results)
        {
            var merged = new List<TItem>(capacity);
            var servingPeerIds = new HashSet<Guid>();
            foreach (var part in results)
            {
                if (part.Items is not null) merged.AddRange(part.Items);
                servingPeerIds.Add(part.PeerId);
            }
            return (merged, servingPeerIds);
        }
    }
}
