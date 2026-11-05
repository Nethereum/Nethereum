using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    internal static class BytecodeFetchRounds
    {
        internal static async Task<int> RunAsync(
            HashSet<byte[]> missing,
            int maxNoProgressRounds,
            int retryDelayMs,
            Func<List<byte[]>, CancellationToken, Task<IReadOnlyList<byte[]>>> fetchChunk,
            Func<byte[], byte[], Task> write,
            CancellationToken ct)
        {
            int noProgress = 0;
            while (missing.Count > 0 && noProgress < maxNoProgressRounds)
            {
                ct.ThrowIfCancellationRequested();
                int before = missing.Count;
                await RunRoundAsync(missing, fetchChunk, write, ct).ConfigureAwait(false);

                if (missing.Count < before) noProgress = 0;
                else
                {
                    noProgress++;
                    if (missing.Count > 0) await Task.Delay(retryDelayMs, ct).ConfigureAwait(false);
                }
            }
            return noProgress;
        }

        private static async Task RunRoundAsync(
            HashSet<byte[]> missing,
            Func<List<byte[]>, CancellationToken, Task<IReadOnlyList<byte[]>>> fetchChunk,
            Func<byte[], byte[], Task> write,
            CancellationToken ct)
        {
            var toFetch = new List<byte[]>(missing);
            foreach (var chunk in SnapByteCodes.Chunks(toFetch))
            {
                ct.ThrowIfCancellationRequested();
                var codes = await fetchChunk(chunk, ct).ConfigureAwait(false);
                foreach (var (hash, code) in SnapByteCodes.Matched(codes, missing.Remove))
                    await write(hash, code).ConfigureAwait(false);
            }
        }
    }
}
