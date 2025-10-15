using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface ILoginSessionState
    {
        bool IsUnlocked { get; }
        event Action? Unlocked;
        Task WaitForUnlockAsync(TimeSpan timeout, CancellationToken ct = default);
        void SetUnlocked();
        void Reset();
    }
    
}