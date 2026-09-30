using System;
using Nethereum.AppChain.Sequencer;
using Nethereum.ChainNode.Hosting.Configuration;

namespace Nethereum.AppChain.Server.Configuration
{
    public static class AppChainServerConfigValidator
    {
        public static void Validate(AppChainServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            ValidateIdentity(config);
            ValidateRpc(config);
            ValidateBlockProduction(config);
            ValidateOperatorKeys(config);
            ValidateDiscovery(config);

            if (config.Consensus.Mode == AppChainConsensusMode.Clique)
                ValidateClique(config);
        }

        public static bool FollowsAnotherNode(AppChainServerConfig config) =>
            !config.Consensus.Sequencer.CanSign && !config.Consensus.Clique.Signer.CanSign;

        private static void ValidateIdentity(AppChainServerConfig config)
        {
            if (config.ChainId <= 0)
                throw new InvalidOperationException("Chain ID must be positive");
        }

        private static void ValidateRpc(AppChainServerConfig config)
        {
            var port = config.Node.Rpc.Port;
            if (port <= 0 || port > 65535)
                throw new InvalidOperationException("Port must be between 1 and 65535");
        }

        private static void ValidateBlockProduction(AppChainServerConfig config)
        {
            if (config.Consensus.BlockProductionMode == BlockProductionMode.Interval
                && config.Consensus.BlockTimeMs <= 0)
                throw new InvalidOperationException("Block time must be positive");
        }

        private static void ValidateOperatorKeys(AppChainServerConfig config)
        {
            if (FollowsAnotherNode(config))
            {
                RequireIdentified(config.Genesis.Owner,
                    "Genesis owner address is required for follower mode (--genesis-owner-address or --genesis-owner-key)");
                RequireIdentified(config.Consensus.Sequencer,
                    "Sequencer address is required for follower mode (--sequencer-address or --sequencer-key)");
                return;
            }

            RequireCanSign(config.Genesis.Owner,
                "Genesis owner private key is required for sequencer mode (--genesis-owner-key)");
            RequireCanSign(config.Consensus.Sequencer,
                "Sequencer private key is required for sequencer mode (--sequencer-key)");
        }


        private static void ValidateDiscovery(AppChainServerConfig config) =>
            ChainNodeDiscoveryValidator.RefuseIfRequested(config.Node.Network, "AppChain");

        private static void ValidateClique(AppChainServerConfig config)
        {
            if (!config.Consensus.Clique.Signer.CanSign && !FollowsAnotherNode(config))
                throw new InvalidOperationException("Signer private key is required for Clique consensus (--signer-key)");

            if (config.Consensus.Clique.InitialSigners.Length == 0)
                throw new InvalidOperationException("At least one initial signer is required for Clique consensus (--initial-signers)");

            if (!HasAPeerToReach(config))
                throw new InvalidOperationException("Clique consensus requires DevP2P peer enodes");

            if (config.Node.Sync.Mode == ChainNode.Hosting.Configuration.SyncMode.None)
                throw new InvalidOperationException(
                    "A Clique signer imports its siblings' blocks, so Sync.Mode may not be None");
        }

        private static bool HasAPeerToReach(AppChainServerConfig config) =>
            config.Node.ResolveDialEnodes().Count > 0;

        private static void RequireIdentified(SignerKeyPair keys, string message)
        {
            if (!keys.IsIdentified) throw new InvalidOperationException(message);
        }

        private static void RequireCanSign(SignerKeyPair keys, string message)
        {
            if (!keys.CanSign) throw new InvalidOperationException(message);
        }
    }
}
