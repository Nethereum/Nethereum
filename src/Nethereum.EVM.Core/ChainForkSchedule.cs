using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.EVM
{
    public sealed class ForkActivationEntry
    {
        public string Fork { get; set; } = "";

        public long? Block { get; set; }

        public ulong? Timestamp { get; set; }

        public bool CountsTowardForkIdentity { get; set; } = true;

        public ForkActivation Resolve()
        {
            var fork = HardforkNames.Parse(Fork);
            if (Timestamp.HasValue) return ForkActivation.AtTimestamp(fork, Timestamp.Value, CountsTowardForkIdentity);
            if (Block.HasValue) return ForkActivation.AtBlock(fork, Block.Value, CountsTowardForkIdentity);

            return ForkActivation.Unscheduled(fork);
        }
    }

    /// <summary>
    /// What a chain IS: its id and the forks it runs. Business logic, and a property of the
    /// chain rather than of any node that happens to run it - two nodes on one chain that
    /// disagree here produce blocks each other rejects. It is deliberately not part of a
    /// node's own configuration, which describes how a process runs rather than what the
    /// chain is.
    ///
    /// <para>Everything a chain's rules are needed for derives from this single statement:
    /// the activations that resolve which fork applies at a point, and the EIP-2124 fork-id
    /// thresholds that identify the chain to a peer.</para>
    /// </summary>
    public sealed class ChainForkSchedule
    {
        public long ChainId { get; set; }

        public string Hardfork { get; set; }

        public string GenesisFork { get; set; } = HardforkName.Frontier.ToString();

        public List<ForkActivationEntry> Schedule { get; set; } = new List<ForkActivationEntry>();

        public static ChainForkSchedule Running(long chainId, HardforkName fork) =>
            new ChainForkSchedule { ChainId = chainId, Hardfork = fork.ToString() };

        public IChainActivations ResolveActivations(ChainActivationsRegistry knownChains = null)
        {
            if (Schedule.Any())
                return new ScheduledChainActivations(
                    HardforkNames.Parse(GenesisFork), Schedule.Select(a => a.Resolve()));

            if (!string.IsNullOrEmpty(Hardfork))
                return ScheduledChainActivations.RunningOnly(HardforkNames.Parse(Hardfork));

            return (knownChains ?? ChainActivationsRegistry.Instance).Get(ChainId);
        }

        /// <summary>
        /// EIP-2124 fork identity, derived from the same schedule the rules come from rather
        /// than restated beside it. Mainnet carried these numbers twice - once for rules and
        /// once for fork-id - and they drifted.
        /// </summary>
        public ForkIdentityThresholds ForkThresholds(ChainActivationsRegistry knownChains = null)
        {
            var scheduled = ResolveActivations(knownChains) is ScheduledChainActivations resolved
                ? resolved.Scheduled
                    .OrderBy(a => a.Timestamp.HasValue)
                    .ThenBy(a => a.Block ?? 0)
                    .ThenBy(a => a.Timestamp ?? 0)
                : Enumerable.Empty<ForkActivation>();

            var activations = scheduled
                .Where(a => a.IsScheduled && a.CountsTowardForkIdentity).ToList();

            return new ForkIdentityThresholds(
                activations.Where(a => a.Block.HasValue).Select(a => (ulong)a.Block.Value).ToArray(),
                activations.Where(a => a.Timestamp.HasValue).Select(a => a.Timestamp.Value).ToArray());
        }
    }

    public sealed class ForkPreview
    {
        public string Fork { get; set; }

        public long? FromBlock { get; set; }

        public ulong? FromTimestamp { get; set; }

        public bool IsStated => !string.IsNullOrEmpty(Fork);

        public ForkActivation Resolve()
        {
            var fork = HardforkNames.Parse(Fork);
            if (FromTimestamp.HasValue) return ForkActivation.AtTimestamp(fork, FromTimestamp.Value);

            return ForkActivation.AtBlock(fork, FromBlock ?? 0);
        }
    }

    public static class ChainRules
    {
        public static IChainActivations ForConsensus(
            ChainForkSchedule chain, ChainActivationsRegistry knownChains = null)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return chain.ResolveActivations(knownChains);
        }

        public static IChainActivations ForSimulation(
            ChainForkSchedule chain, ForkPreview preview = null,
            ChainActivationsRegistry knownChains = null)
        {
            var actual = ForConsensus(chain, knownChains);

            return preview != null && preview.IsStated
                ? new PreviewingChainActivations(actual, preview.Resolve())
                : actual;
        }
    }
}
