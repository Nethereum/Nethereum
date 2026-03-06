using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing
{
    public static class RetryRunner
    {
        public static async Task RunWithExponentialBackoffAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken,
            Action<Exception, int, int> onRetry = null,
            int initialDelaySeconds = 5,
            int maxDelaySeconds = 300)
        {
            var retryCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await action(cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    var shift = Math.Min(retryCount, 30);
                    var delaySeconds = (int)Math.Min((long)initialDelaySeconds << shift, maxDelaySeconds);
                    onRetry?.Invoke(ex, retryCount + 1, delaySeconds);
                    try
                    {
                        await Task.Delay(delaySeconds * 1000, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    retryCount++;
                }
            }
        }
    }
}
