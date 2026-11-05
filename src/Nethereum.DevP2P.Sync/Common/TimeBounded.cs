using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Common
{
    internal static class TimeBounded
    {
        internal static async Task<(bool TimedOut, T Value)> RunAsync<T>(
            Func<CancellationToken, Task<T>> call, TimeSpan limit, CancellationToken ct)
        {
            using var boundedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            boundedCts.CancelAfter(limit);
            try
            {
                var value = await call(boundedCts.Token).WaitAsync(boundedCts.Token).ConfigureAwait(false);
                return (false, value);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return (true, default);
            }
        }
    }
}
