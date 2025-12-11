using System;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    public class LightClientRecording
    {
        public string BeaconBaseUrl { get; set; } = string.Empty;
        public string ExecutionRpcUrl { get; set; } = string.Empty;
        public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
        public ulong FinalizedSlot { get; set; }
        public ulong FinalizedBlockNumber { get; set; }
        public string FinalizedBlockHash { get; set; } = string.Empty;
        public string WeakSubjectivityRoot { get; set; } = string.Empty;
        public ulong SyncCommitteePeriod { get; set; }
        public string AccountAddress { get; set; } = string.Empty;
        public string StorageSlot { get; set; } = string.Empty;
        public string BootstrapResponse { get; set; } = string.Empty;
        public string UpdatesResponse { get; set; } = string.Empty;
        public string AccountProofResponse { get; set; } = string.Empty;
    }
}
