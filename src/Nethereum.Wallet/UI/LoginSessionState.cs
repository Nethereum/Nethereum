using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public class LoginSessionState : ILoginSessionState
        {
            private TaskCompletionSource<bool> _tcs = NewTcs();
            public bool IsUnlocked { get; private set; }
            public event Action? Unlocked;
            public Task WaitForUnlockAsync(TimeSpan timeout, CancellationToken ct = default)
            {
                if (IsUnlocked) return Task.CompletedTask;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
#if NETSTANDARD2_0
                return WaitForUnlockLegacyAsync(cts.Token);
#else
                return _tcs.Task.WaitAsync(cts.Token);
#endif
            }
            public void SetUnlocked()
            {
                if (IsUnlocked) return;
                IsUnlocked = true;
                _tcs.TrySetResult(true);
                Unlocked?.Invoke();
            }
            public void Reset()
            {
                IsUnlocked = false;
                _tcs = NewTcs();
            }
            private static TaskCompletionSource<bool> NewTcs() =>
                new(TaskCreationOptions.RunContinuationsAsynchronously);

#if NETSTANDARD2_0
            private async Task WaitForUnlockLegacyAsync(CancellationToken token)
            {
                var delayTask = Task.Delay(Timeout.InfiniteTimeSpan, token);
                var completedTask = await Task.WhenAny(_tcs.Task, delayTask).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await completedTask.ConfigureAwait(false);
            }
#endif
        }
    
}
