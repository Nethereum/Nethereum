using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage
{
    public sealed record HeaderSyncState
    {
        public required ulong SchemaVersion { get; init; }

        public required IReadOnlyList<HeaderSubchain> Subchains { get; init; }

        public static HeaderSyncState Empty { get; } = new()
        {
            SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
            Subchains = new List<HeaderSubchain>(),
        };
    }

    public sealed record HeaderSubchain
    {
        public required ulong Head { get; init; }

        public required ulong Tail { get; init; }

        public required ulong Next { get; init; }
    }

    public interface IHeaderSyncStateEncoder
    {
        byte[] Encode(HeaderSyncState state);
        HeaderSyncState Decode(byte[] data);
    }
}
