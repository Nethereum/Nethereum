using System;

namespace Nethereum.AppChain.Server.Configuration
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainConsensusMode")]
    public enum AppChainConsensusMode
    {
        SingleSequencer,

        Clique,
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "AppChainConsensusModeParser")]
    public static class AppChainConsensusModeParser
    {
        public const string SingleSequencerName = "single-sequencer";

        public const string CliqueName = "clique";

        public static AppChainConsensusMode Parse(string? name)
        {
            if (string.IsNullOrEmpty(name)) return AppChainConsensusMode.SingleSequencer;

            if (string.Equals(name, SingleSequencerName, StringComparison.OrdinalIgnoreCase))
                return AppChainConsensusMode.SingleSequencer;

            if (string.Equals(name, CliqueName, StringComparison.OrdinalIgnoreCase))
                return AppChainConsensusMode.Clique;

            throw new InvalidOperationException(
                $"Consensus mode must be '{SingleSequencerName}' or '{CliqueName}'");
        }

        public static string NameOf(AppChainConsensusMode mode) =>
            mode == AppChainConsensusMode.Clique ? CliqueName : SingleSequencerName;
    }
}
