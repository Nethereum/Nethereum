using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.VerifiedState
{
    public interface IVerifiedBalanceService
    {
        Task<bool> IsAvailableAsync(BigInteger chainId);
        Task<VerifiedBalanceResult> GetBalanceAsync(string address, BigInteger chainId);
        Task<LightClientStatus> GetStatusAsync(BigInteger chainId);
        event EventHandler<LightClientStatusChangedEventArgs> StatusChanged;
    }

    public enum VerifiedBalanceMode
    {
        Finalized,
        Optimistic,
        Unavailable
    }

    public class VerifiedBalanceResult
    {
        public BigInteger Balance { get; set; }
        public bool IsVerified { get; set; }
        public ulong BlockNumber { get; set; }
        public VerifiedBalanceMode Mode { get; set; }
        public string Error { get; set; }
        public bool IsRpcLimitation { get; set; }

        // Finalized balance (strongest security - ~12 min behind)
        public BigInteger? FinalizedBalance { get; set; }
        public ulong FinalizedBlockNumber { get; set; }
        public bool HasFinalizedBalance { get; set; }
        public string FinalizedError { get; set; }

        // Optimistic balance (weaker security - ~seconds behind)
        public BigInteger? OptimisticBalance { get; set; }
        public ulong OptimisticBlockNumber { get; set; }
        public bool HasOptimisticBalance { get; set; }
        public string OptimisticError { get; set; }
    }

    public class LightClientStatus
    {
        public bool IsInitialized { get; set; }
        public bool IsSyncing { get; set; }
        public ulong FinalizedSlot { get; set; }
        public string Error { get; set; }
    }

    public class LightClientStatusChangedEventArgs : EventArgs
    {
        public BigInteger ChainId { get; }
        public LightClientStatus Status { get; }

        public LightClientStatusChangedEventArgs(BigInteger chainId, LightClientStatus status)
        {
            ChainId = chainId;
            Status = status;
        }
    }
}
