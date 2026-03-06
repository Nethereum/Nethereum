using System;
using System.Numerics;

namespace Nethereum.AppChain.Sync
{
    public class LiveBlockSyncConfig
    {
        public string SequencerRpcUrl { get; set; } = "";
        public int PollIntervalMs { get; set; } = 1000;
        public int ErrorRetryDelayMs { get; set; } = 5000;
        public bool AutoFollow { get; set; } = true;
        public bool RejectOnStateRootMismatch { get; set; } = true;

        public static LiveBlockSyncConfig Default => new()
        {
            PollIntervalMs = 1000,
            ErrorRetryDelayMs = 5000,
            AutoFollow = true,
            RejectOnStateRootMismatch = true
        };
    }

    public class StateRootMismatchEventArgs : EventArgs
    {
        public BigInteger BlockNumber { get; set; }
        public byte[]? ExpectedStateRoot { get; set; }
        public byte[]? ComputedStateRoot { get; set; }
    }

    public class InvalidBlockException : Exception
    {
        public InvalidBlockException(string message) : base(message) { }
        public InvalidBlockException(string message, Exception innerException) : base(message, innerException) { }
    }
}
