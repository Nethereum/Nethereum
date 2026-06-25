using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Persisted progress of the backward-walking header skeleton. Instead of a single
    /// <see cref="IChainMetadataStore.GetLastFetchedHeader"/> cursor — which can only describe one
    /// contiguous run — this records the set of header segments laid down so far, each identifiable
    /// by its <c>[Tail..Head]</c> block range plus the next block to fetch. Disjoint segments arise
    /// when the trusted tip advances (a new segment opens at the new tip while the older one is still
    /// below it); when a lower walk links onto an older segment's <see cref="HeaderSubchain.Head"/>
    /// the two merge. This is what lets a restart, or a tip advance, resume the open segments and fill
    /// only the bounded gap rather than re-walk the whole chain from the tip.
    /// </summary>
    public sealed record HeaderSyncState
    {
        /// <summary>Bumped on any breaking change to the persisted shape. The reader treats an unknown
        /// version as <see cref="Empty"/> (no progress) and re-derives.</summary>
        public required ulong SchemaVersion { get; init; }

        /// <summary>The validated header segments, conventionally ordered newest (highest <see cref="HeaderSubchain.Head"/>)
        /// first, mirroring the way the skeleton extends from the latest trusted tip downward.</summary>
        public required IReadOnlyList<HeaderSubchain> Subchains { get; init; }

        public static HeaderSyncState Empty { get; } = new()
        {
            SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
            Subchains = new List<HeaderSubchain>(),
        };
    }

    /// <summary>
    /// One contiguous, parent-hash-validated run of headers. The skeleton fetches headers downward
    /// from <see cref="Head"/>, lowering <see cref="Tail"/> as each batch validates, until the run
    /// links onto an older segment or genesis.
    /// </summary>
    public sealed record HeaderSubchain
    {
        /// <summary>Highest block number in this segment (inclusive) — a trusted tip the segment is anchored to.</summary>
        public required ulong Head { get; init; }

        /// <summary>Lowest block number validated in this segment so far (inclusive). Decreases as the skeleton walks down.</summary>
        public required ulong Tail { get; init; }

        /// <summary>Next block number to request, walking downward (normally <see cref="Tail"/> - 1).
        /// The segment is complete once the walk reaches genesis or links onto an older segment's <see cref="Head"/>.</summary>
        public required ulong Next { get; init; }
    }

    /// <summary>Codec for <see cref="HeaderSyncState"/> — RLP, consistent with the other
    /// <c>chain_metadata</c> rows. Returns null from <see cref="Decode"/> on an unknown schema version.</summary>
    public interface IHeaderSyncStateEncoder
    {
        byte[] Encode(HeaderSyncState state);
        HeaderSyncState Decode(byte[] data);
    }
}
