using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "One blob-schedule entry, by fork or by activation timestamp")]
    public readonly struct BlobScheduleEntry
    {
        private BlobScheduleEntry(string name, HardforkName? fork, ulong? activationTimestamp, BlobParameters parameters)
        {
            Name = name;
            Fork = fork;
            ActivationTimestamp = activationTimestamp;
            Parameters = parameters;
        }

        public string Name { get; }

        public HardforkName? Fork { get; }

        public ulong? ActivationTimestamp { get; }

        public BlobParameters Parameters { get; }

        public static BlobScheduleEntry ForFork(HardforkName fork, BlobParameters parameters) =>
            new BlobScheduleEntry(fork.ToString().ToLowerInvariant(), fork, null, parameters);

        public static BlobScheduleEntry AtTimestamp(string name, ulong activationTimestamp, BlobParameters parameters)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A scheduled blob entry needs a name.", nameof(name));
            return new BlobScheduleEntry(name, null, activationTimestamp, parameters);
        }
    }

    [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "The chain blob schedule and its per-timestamp resolution")]
    public sealed class BlobSchedule
    {
        public static readonly BlobSchedule Empty = new BlobSchedule(new BlobScheduleEntry[0]);

        public BlobSchedule(IEnumerable<BlobScheduleEntry> entries)
        {
            Entries = (entries ?? throw new ArgumentNullException(nameof(entries))).ToList();
        }

        public IReadOnlyList<BlobScheduleEntry> Entries { get; }

        public IEnumerable<BlobScheduleEntry> TimedEntries => Entries.Where(e => e.ActivationTimestamp.HasValue);

        public BlobParameters? At(HardforkName protocolFork, ulong protocolForkActivatedAt, ulong timestamp)
        {
            BlobScheduleEntry? latestTimed = null;
            foreach (var entry in TimedEntries)
            {
                var activation = entry.ActivationTimestamp.Value;
                if (activation > timestamp || activation < protocolForkActivatedAt) continue;
                if (latestTimed == null || activation >= latestTimed.Value.ActivationTimestamp.Value)
                    latestTimed = entry;
            }

            if (latestTimed != null) return latestTimed.Value.Parameters;

            foreach (var entry in Entries)
                if (entry.Fork == protocolFork) return entry.Parameters;

            return null;
        }
    }
}
