using System;

namespace Nethereum.AppChain.Server.Configuration
{
    public enum AppChainConsensusMode
    {
        SingleSequencer,

        Clique,
    }

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
