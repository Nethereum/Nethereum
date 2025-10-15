using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public class LoginPromptService : ILoginPromptService
    {
        private readonly IWalletVaultService _vaultService;
        private readonly ILoginSessionState _sessionState;

        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);

        // Single-flight wait task for concurrent PromptLoginAsync callers.
        private Task<bool>? _waitTask;

        // 0 = not raised, 1 = raised (ensures event only fires once per locked cycle).
        private int _loginRequestedRaised;

        // Protects transition into a new waiting state (not the whole wait itself).
        private readonly SemaphoreSlim _gate = new(1, 1);

        public event Action? LoginUIRequested;

        public LoginPromptService(IWalletVaultService vaultService,
                                  ILoginSessionState sessionState)
        {
            _vaultService = vaultService;
            _sessionState = sessionState;
        }

        public Task<bool> PromptLoginAsync()
        {
            // Fast-path if already unlocked / vault loaded.
            if (IsUnlocked())
            {
                _sessionState.SetUnlocked();
                return Task.FromResult(true);
            }

            // If a wait is already in-flight, reuse it (no locking in the hot path if present).
            var existing = Volatile.Read(ref _waitTask);
            if (existing != null) return existing;

            // Enter guarded section to create a new wait if still needed.
            return StartOrReuseWaitAsync();
        }

        private async Task<bool> StartOrReuseWaitAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsUnlocked())
                {
                    _sessionState.SetUnlocked();
                    return true;
                }

                // Another thread might have initialized the wait while we awaited the gate.
                if (_waitTask != null) return await _waitTask.ConfigureAwait(false);

                // Create new wait task.
                _waitTask = WaitForUnlockInternalAsync();

                // Ensure UI is prompted exactly once per lock cycle.
                if (Interlocked.Exchange(ref _loginRequestedRaised, 1) == 0)
                {
                    var handler = LoginUIRequested;
                    handler?.Invoke();
                }

                return await _waitTask.ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<bool> WaitForUnlockInternalAsync()
        {
            try
            {
                // Leverage session state's own wait (it should complete when unlocked).
                await _sessionState.WaitForUnlockAsync(_timeout).ConfigureAwait(false);
                return IsUnlocked();
            }
            catch
            {
                return false;
            }
            finally
            {
                // Allow a future PromptLoginAsync to initiate a new cycle if still locked.
                if (!IsUnlocked())
                {
                    // Reset single-flight state (another attempt can re-raise LoginUIRequested).
                    Volatile.Write(ref _waitTask, null);
                    Interlocked.Exchange(ref _loginRequestedRaised, 0);
                }
            }
        }

        public async Task LogoutAsync()
        {
            await _vaultService.LockAsync().ConfigureAwait(false);
            _sessionState.Reset();

            // Reset cycle so a fresh PromptLoginAsync will raise event again.
            Volatile.Write(ref _waitTask, null);
            Interlocked.Exchange(ref _loginRequestedRaised, 0);
        }

        private bool IsUnlocked() =>
            _sessionState.IsUnlocked || _vaultService.GetCurrentVault() != null;
    }
}