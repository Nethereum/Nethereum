using System;

namespace Nethereum.MainnetChain.Configuration
{
    public static class ConsensusStartupGuard
    {
        public enum Decision
        {
            Verified,

            UnverifiedAllowed,

            RefuseNoBeacon,

            EngineDriven,

            RefuseEngineWithLightClient,

            RefuseGenesisWithoutEngine,
        }

        public static Decision Evaluate(MainnetChainServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var hasBeacon = !string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint);
            if (!string.IsNullOrWhiteSpace(config.GenesisPath) && !config.Engine.Enabled)
                return Decision.RefuseGenesisWithoutEngine;
            if (config.Engine.Enabled)
                return hasBeacon ? Decision.RefuseEngineWithLightClient : Decision.EngineDriven;
            if (hasBeacon) return Decision.Verified;
            return config.AllowUnverifiedConsensus ? Decision.UnverifiedAllowed : Decision.RefuseNoBeacon;
        }

        public const string RefusalMessage =
            "Refusing to start: no beacon light client is configured (--beacon <URL>), so blocks would be " +
            "accepted from peers WITHOUT consensus verification. Set --beacon for a consensus-verified mainnet " +
            "follower, or pass --allow-unverified-consensus to run unverified anyway (NOT recommended for mainnet).";

        public const string UnverifiedWarning =
            "WARNING: running WITHOUT a beacon light client (--allow-unverified-consensus). The consensus gate is " +
            "permissive — this follower accepts whatever its peers serve, with no independent consensus " +
            "verification. Set --beacon <URL> for a consensus-verified mainnet follower.";

        public const string EngineWithLightClientMessage =
            "Refusing to start: the Engine API (--engine) and the beacon light client (--beacon) are mutually exclusive. " +
            "In Engine mode the consensus client drives the follower through forkchoiceUpdated, and the light client " +
            "would be a second, competing source of the canonical tip.";

        public const string GenesisWithoutEngineMessage =
            "Refusing to start: --genesis selects a custom chain that has no peers to sync from, so it runs " +
            "only under the Engine API. Add --engine, or drop --genesis for the mainnet follower.";
    }
}
