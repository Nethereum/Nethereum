using Nethereum.AppChain.Sync;

namespace Nethereum.AppChain.Sequencer
{
    public class AppChainReplicaConfig
    {
        public string SequencerRpcUrl { get; set; } = "";

        public int TxConfirmationTimeoutMs { get; set; } = 30000;

        public int TxPollIntervalMs { get; set; } = 500;

        public bool AutoStartSync { get; set; } = true;

        public CoordinatedSyncConfig SyncConfig { get; set; } = CoordinatedSyncConfig.Default;

        public LiveBlockSyncConfig LiveSyncConfig { get; set; } = LiveBlockSyncConfig.Default;

        public static AppChainReplicaConfig Default => new()
        {
            TxConfirmationTimeoutMs = 30000,
            TxPollIntervalMs = 500,
            AutoStartSync = true,
            SyncConfig = CoordinatedSyncConfig.Default,
            LiveSyncConfig = LiveBlockSyncConfig.Default
        };

        public static AppChainReplicaConfig ForSequencer(string sequencerRpcUrl) => new()
        {
            SequencerRpcUrl = sequencerRpcUrl,
            TxConfirmationTimeoutMs = 30000,
            TxPollIntervalMs = 500,
            AutoStartSync = true,
            SyncConfig = CoordinatedSyncConfig.Default,
            LiveSyncConfig = new LiveBlockSyncConfig
            {
                SequencerRpcUrl = sequencerRpcUrl,
                PollIntervalMs = 1000,
                AutoFollow = true
            }
        };
    }
}
