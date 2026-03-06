using System.Numerics;

namespace Nethereum.AppChain.Server.Configuration
{
    public class AppChainServerConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8546;

        public BigInteger ChainId { get; set; } = 420420;
        public string ChainName { get; set; } = "AppChain";

        public string? GenesisOwnerPrivateKey { get; set; }
        public string? GenesisOwnerAddress { get; set; }

        public string? SequencerPrivateKey { get; set; }
        public string? SequencerAddress { get; set; }

        public int BlockTimeMs { get; set; } = 1000;
        public bool AllowEmptyBlocks { get; set; } = false;

        public bool DeployMudWorld { get; set; } = true;
        public byte[] MudWorldSalt { get; set; } = new byte[32];

        public string DatabasePath { get; set; } = "./appchain-data";
        public bool UseInMemoryStorage { get; set; } = false;

        public string? L1RpcUrl { get; set; }
        public string? AnchorContractAddress { get; set; }
        public int AnchorCadence { get; set; } = 100;

        public string? BatchOutputDirectory { get; set; }
        public string? SnapshotOutputDirectory { get; set; }
        public int BatchSize { get; set; } = 100;
        public int SnapshotInterval { get; set; } = 10000;
        public bool AutoCreateBatches { get; set; } = false;

        // Consensus mode: "single-sequencer" or "clique"
        public string ConsensusMode { get; set; } = "single-sequencer";

        // P2P mode: "none" or "dotnetty"
        public string P2PMode { get; set; } = "none";

        // P2P configuration (used when P2PMode != "none")
        public int P2PPort { get; set; } = 30303;
        public string P2PListenAddress { get; set; } = "0.0.0.0";
        public string[] BootstrapNodes { get; set; } = Array.Empty<string>();

        // Clique configuration (used when ConsensusMode == "clique")
        public string? SignerPrivateKey { get; set; }
        public string? SignerAddress { get; set; }
        public string[] InitialSigners { get; set; } = Array.Empty<string>();
        public int CliquePeriod { get; set; } = 15;
        public int CliqueEpoch { get; set; } = 30000;

        // HTTP Sync configuration (alternative to P2P)
        public string[] SyncPeers { get; set; } = Array.Empty<string>();
        public int SyncPollIntervalMs { get; set; } = 1000;
        public bool AutoSyncOnStart { get; set; } = true;
        public bool EnableStateSync { get; set; } = true;

        // Cross-chain messaging configuration
        public bool EnableMessaging { get; set; } = false;
        public string[] HubSourceChains { get; set; } = Array.Empty<string>(); // format: "chainId:rpcUrl:hubAddress"
        public int MessagePollIntervalMs { get; set; } = 5000;
        public int MaxMessagesPerPoll { get; set; } = 100;
        public bool EnableMessageAcknowledgment { get; set; } = false;
        public int AcknowledgmentIntervalMs { get; set; } = 30000;

        public string? OtlpEndpoint { get; set; }

        public string RpcUrl => $"http://{Host}:{Port}";

        public bool IsFollowerMode => SyncPeers.Length > 0;

        public void DeriveAddresses()
        {
            // Derive address from private key only if address not already set
            if (!string.IsNullOrEmpty(GenesisOwnerPrivateKey) && string.IsNullOrEmpty(GenesisOwnerAddress))
            {
                var key = new Signer.EthECKey(GenesisOwnerPrivateKey);
                GenesisOwnerAddress = key.GetPublicAddress();
            }

            if (!string.IsNullOrEmpty(SequencerPrivateKey) && string.IsNullOrEmpty(SequencerAddress))
            {
                var key = new Signer.EthECKey(SequencerPrivateKey);
                SequencerAddress = key.GetPublicAddress();
            }

            if (!string.IsNullOrEmpty(SignerPrivateKey) && string.IsNullOrEmpty(SignerAddress))
            {
                var key = new Signer.EthECKey(SignerPrivateKey);
                SignerAddress = key.GetPublicAddress();
            }
        }

        public void Validate()
        {
            // For follower mode, we only need addresses (not private keys)
            if (IsFollowerMode)
            {
                if (string.IsNullOrEmpty(GenesisOwnerAddress))
                    throw new InvalidOperationException("Genesis owner address is required for follower mode (--genesis-owner-address or --genesis-owner-key)");

                if (string.IsNullOrEmpty(SequencerAddress))
                    throw new InvalidOperationException("Sequencer address is required for follower mode (--sequencer-address or --sequencer-key)");
            }
            else
            {
                // Sequencer mode requires private keys
                if (string.IsNullOrEmpty(GenesisOwnerPrivateKey))
                    throw new InvalidOperationException("Genesis owner private key is required for sequencer mode (--genesis-owner-key)");

                if (string.IsNullOrEmpty(SequencerPrivateKey))
                    throw new InvalidOperationException("Sequencer private key is required for sequencer mode (--sequencer-key)");
            }

            if (ChainId <= 0)
                throw new InvalidOperationException("Chain ID must be positive");

            if (Port <= 0 || Port > 65535)
                throw new InvalidOperationException("Port must be between 1 and 65535");

            if (BlockTimeMs <= 0)
                throw new InvalidOperationException("Block time must be positive");

            if (ConsensusMode != "single-sequencer" && ConsensusMode != "clique")
                throw new InvalidOperationException("Consensus mode must be 'single-sequencer' or 'clique'");

            if (P2PMode != "none" && P2PMode != "dotnetty")
                throw new InvalidOperationException("P2P mode must be 'none' or 'dotnetty'");

            if (ConsensusMode == "clique")
            {
                if (string.IsNullOrEmpty(SignerPrivateKey) && !IsFollowerMode)
                    throw new InvalidOperationException("Signer private key is required for Clique consensus (--signer-key)");

                if (InitialSigners.Length == 0)
                    throw new InvalidOperationException("At least one initial signer is required for Clique consensus (--initial-signers)");

                if (P2PMode == "none" && SyncPeers.Length == 0)
                    throw new InvalidOperationException("Clique consensus requires either P2P (--p2p dotnetty) or HTTP sync peers (--sync-peers)");
            }
        }
    }
}
