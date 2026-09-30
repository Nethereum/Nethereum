using System.Numerics;
using Nethereum.ChainNode.Hosting.Configuration;

namespace Nethereum.AppChain.Server.Configuration
{
    public class AppChainServerConfig
    {
        public const string DefaultAppChainHardfork = "amsterdam";

        public BigInteger ChainId { get; set; } = 420420;

        public Nethereum.EVM.ChainForkSchedule ForkSchedule { get; set; } =
            new Nethereum.EVM.ChainForkSchedule { ChainId = 420420, Hardfork = DefaultAppChainHardfork };

        public string ChainName { get; set; } = "AppChain";

        public BigInteger? BaseFee { get; set; }

        public string? OtlpEndpoint { get; set; }

        public ChainNodeConfig Node { get; set; } = DefaultNode();

        public AppChainGenesisConfig Genesis { get; set; } = new AppChainGenesisConfig();

        public AppChainConsensusConfig Consensus { get; set; } = new AppChainConsensusConfig();

        public AppChainAnchoringConfig Anchoring { get; set; } = new AppChainAnchoringConfig();

        public AppChainMessagingConfig Messaging { get; set; } = new AppChainMessagingConfig();

        public AppChainMudConfig Mud { get; set; } = new AppChainMudConfig();


        public string RpcUrl => Node.Rpc.Url;

        public bool ImportsBlocks => Node.Sync.Mode != SyncMode.None;

        public bool ProducesBlocks => Consensus.Mode == AppChainConsensusMode.Clique
            ? Consensus.Clique.Signer.CanSign
            : Consensus.Sequencer.CanSign;

        public bool IsFullyUnconfigured =>
            !Genesis.Owner.IsIdentified && !Genesis.Owner.CanSign
            && !Consensus.Sequencer.IsIdentified && !Consensus.Sequencer.CanSign
            && !Consensus.Clique.Signer.IsIdentified && !Consensus.Clique.Signer.CanSign;

        public void DeriveAddresses()
        {
            Genesis.Owner.DeriveAddressFromPrivateKey();
            Consensus.Sequencer.DeriveAddressFromPrivateKey();
            Consensus.Clique.Signer.DeriveAddressFromPrivateKey();
        }

        public void Validate() => AppChainServerConfigValidator.Validate(this);

        private static ChainNodeConfig DefaultNode() =>
            new ChainNodeConfig
            {
                Rpc = new ChainNodeRpcConfig { Port = 8546 },
                Storage = new ChainNodeStorageConfig { DataDirectory = "./appchain-data" },
                Network = new ChainNodeNetworkConfig
                {
                    ListenPort = 30403,
                    Serve = false,
                    Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = true, DisableDiscv5 = true },
                },
                Sync = new ChainNodeSyncConfig
                {
                    EnablePushedBlocks = true,
                    FloorTargetPeerCountByDialPool = true,
                    CheckpointEvery = 0,
                },
                Mempool = new ChainNodeMempoolConfig { EnableTrustedPeerAdmission = true },
            };
    }
}
