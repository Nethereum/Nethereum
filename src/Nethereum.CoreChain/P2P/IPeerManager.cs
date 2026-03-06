using System;
using System.Collections.Generic;

namespace Nethereum.CoreChain.P2P
{
    public interface IRateLimiter : IDisposable
    {
        void Start();
        void Stop();

        RateLimitResult CheckLimit(string peerId, RateLimitCategory category);
        void RecordRequest(string peerId, RateLimitCategory category);
        void Reset(string peerId);
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan RetryAfter { get; set; }

        public static RateLimitResult Allowed(int remaining = int.MaxValue) => new()
        {
            IsAllowed = true,
            RemainingRequests = remaining
        };

        public static RateLimitResult Denied(TimeSpan retryAfter) => new()
        {
            IsAllowed = false,
            RemainingRequests = 0,
            RetryAfter = retryAfter
        };
    }

    public enum RateLimitCategory
    {
        Messages,
        Blocks,
        Transactions,
        Connections
    }

    public class RateLimitConfig
    {
        public int MessagesPerSecond { get; set; } = 100;
        public int BlocksPerMinute { get; set; } = 60;
        public int TransactionsPerSecond { get; set; } = 100;
        public int ConnectionsPerMinute { get; set; } = 10;
        public int CleanupIntervalMs { get; set; } = 60000;

        public static RateLimitConfig Default => new();
    }

    public interface IReputationManager : IDisposable
    {
        void Start();
        void Stop();

        int GetReputation(string peerId);
        void RecordPositive(string peerId, ReputationEvent eventType);
        void RecordNegative(string peerId, ReputationEvent eventType);
        bool IsBanned(string peerId);
        void Ban(string peerId, TimeSpan duration);
        void Unban(string peerId);
        IReadOnlyList<string> GetBannedPeers();
    }

    public enum ReputationEvent
    {
        ValidBlock,
        InvalidBlock,
        ValidTransaction,
        InvalidTransaction,
        Spam,
        Timeout,
        Disconnect,
        ProtocolViolation
    }

    public class ReputationConfig
    {
        public int InitialReputation { get; set; } = 100;
        public int MaxReputation { get; set; } = 200;
        public int MinReputation { get; set; } = -100;
        public int BanThreshold { get; set; } = -50;
        public int BanDurationMinutes { get; set; } = 60;
        public int DecayIntervalMs { get; set; } = 60000;
        public int DecayAmount { get; set; } = 1;

        public static ReputationConfig Default => new();
    }
}
