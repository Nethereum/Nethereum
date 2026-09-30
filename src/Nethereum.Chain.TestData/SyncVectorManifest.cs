using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Chain.TestData
{
    public sealed record BlockManifestEntry(
        long Number,
        string Hash,
        string StateRoot,
        string TransactionsRoot,
        string ReceiptsRoot,
        int TxCount,
        int LogCount);

    public sealed class SyncVectorManifest
    {
        public string VectorName { get; set; } = "";
        public int Version { get; set; }
        public List<BlockManifestEntry> Blocks { get; set; } = new();

        public static SyncVectorManifest From(string name, int version, IEnumerable<ProducedBlock> blocks)
            => new SyncVectorManifest
            {
                VectorName = name,
                Version = version,
                Blocks = blocks.Select(b => new BlockManifestEntry(
                    b.Number,
                    Hex(b.Hash),
                    Hex(b.StateRoot),
                    Hex(b.TransactionsRoot),
                    Hex(b.ReceiptsRoot),
                    b.TxCount,
                    b.LogCount)).ToList()
            };

        private static string Hex(byte[] value) => value == null ? null : value.ToHex(true);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static SyncVectorManifest FromJson(string json)
            => JsonSerializer.Deserialize<SyncVectorManifest>(json, JsonOptions);
    }
}
