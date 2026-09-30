namespace Nethereum.MainnetChain.Hosting
{
    public static class DiscoveryLoop
    {
        public static async Task RunAsync(
            Func<CancellationToken, Task<IReadOnlyList<string>>> harvest,
            Action<string> enqueue,
            TimeSpan interval,
            Action<Exception> onError,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var found = await harvest(ct).ConfigureAwait(false);
                    if (found != null)
                        foreach (var enode in found)
                            enqueue(enode);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    onError(ex);
                }

                try { await Task.Delay(interval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
