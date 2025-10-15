using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public class LoginSessionState : ILoginSessionState
    {
        private TaskCompletionSource _tcs = NewTcs();
        public bool IsUnlocked { get; private set; }
        public event Action? Unlocked;
        public Task WaitForUnlockAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            if (IsUnlocked) return Task.CompletedTask;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            return _tcs.Task.WaitAsync(cts.Token);
        }
        public void SetUnlocked()
        {
            if (IsUnlocked) return;
            IsUnlocked = true;
            _tcs.TrySetResult();
            Unlocked?.Invoke();
        }
        public void Reset()
        {
            IsUnlocked = false;
            _tcs = NewTcs();
        }
        private static TaskCompletionSource NewTcs() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}