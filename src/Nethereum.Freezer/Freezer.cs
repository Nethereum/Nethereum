using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Documentation;

namespace Nethereum.Freezer
{
    public sealed class Freezer : IDisposable, IFrozenReadSource
    {
        private readonly Dictionary<string, FreezerTable<byte[]>> _tables;
        private readonly Dictionary<string, FreezerTailGroup> _tailGroupByName;

        private Freezer(Dictionary<string, FreezerTable<byte[]>> tables,
            Dictionary<string, FreezerTailGroup> tailGroupByName)
        {
            _tables = tables;
            _tailGroupByName = tailGroupByName;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.Open — open an ancient store")]
        public static Freezer Open(FreezerLayout layout, FreezerOpenMode mode)
        {
            var tables = OpenAllTables(layout, mode);
            var tailGroupByName = layout.Tables.ToDictionary(config => config.Name, config => config.TailGroup);

            try
            {
                AlignAcrossTables(tables, tailGroupByName, mode);
                return new Freezer(tables, tailGroupByName);
            }
            catch
            {
                DisposeAll(tables.Values);
                throw;
            }
        }

        private static Dictionary<string, FreezerTable<byte[]>> OpenAllTables(FreezerLayout layout,
            FreezerOpenMode mode)
        {
            var opened = new Dictionary<string, FreezerTable<byte[]>>();
            try
            {
                foreach (var config in layout.Tables)
                    opened[config.Name] = OpenTable(layout, config, mode);
                return opened;
            }
            catch
            {
                DisposeAll(opened.Values);
                throw;
            }
        }

        private static FreezerTable<byte[]> OpenTable(FreezerLayout layout, FreezerTableConfig config,
            FreezerOpenMode mode)
        {
            var paths = new FreezerTablePaths(layout.Directory, config.Name, config.UseCompression);
            var codec = BuildCodec(config);

            return mode == FreezerOpenMode.ReadOnly
                ? FreezerTable<byte[]>.OpenReadOnly(paths, codec)
                : FreezerTable<byte[]>.OpenForAppend(paths, codec, layout.MaxFileSize);
        }

        private static IItemCodec<byte[]> BuildCodec(FreezerTableConfig config)
        {
            return config.UseCompression
                ? new SnappyItemCodec<byte[]>(new IdentityByteCodec())
                : new IdentityByteCodec();
        }

        private static void DisposeAll(IEnumerable<FreezerTable<byte[]>> tables)
        {
            foreach (var table in tables)
                table.Dispose();
        }

        private static void AlignAcrossTables(Dictionary<string, FreezerTable<byte[]>> tables,
            Dictionary<string, FreezerTailGroup> tailGroupByName, FreezerOpenMode mode)
        {
            var head = ComputeHead(tables.Values);

            if (mode == FreezerOpenMode.ReadOnly)
            {
                AssertNonEmptyTablesAtHead(tables.Values, head);
                return;
            }

            FastForwardGenuinelyEmptyTables(tables.Values, head);
            TruncateTablesAheadOfHead(tables.Values, head);
            AlignTailGroups(tables, tailGroupByName);
        }

        private static bool IsGenuinelyEmpty(FreezerTable<byte[]> table) => table.Count == 0 && table.VirtualTail == 0;

        private static long AbsoluteHead(FreezerTable<byte[]> table) => table.VirtualTail + table.Count;

        private static long ComputeHead(IEnumerable<FreezerTable<byte[]>> tables)
        {
            var nonEmpty = tables.Where(table => !IsGenuinelyEmpty(table)).ToList();
            return nonEmpty.Count == 0 ? 0 : nonEmpty.Min(AbsoluteHead);
        }

        private static void AssertNonEmptyTablesAtHead(IEnumerable<FreezerTable<byte[]>> tables, long head)
        {
            foreach (var table in tables.Where(table => !IsGenuinelyEmpty(table)))
            {
                if (AbsoluteHead(table) != head)
                    throw new FreezerValidationException(
                        $"freezer table '{table.Name}' head {AbsoluteHead(table)} != archive head {head}");
            }
        }

        private static void FastForwardGenuinelyEmptyTables(IEnumerable<FreezerTable<byte[]>> tables, long head)
        {
            foreach (var table in tables.Where(IsGenuinelyEmpty))
            {
                if (head == 0)
                    continue;

                Console.Error.WriteLine(
                    $"[Nethereum.Freezer] warning: freezer table '{table.Name}' is genuinely new " +
                    $"(no data, no persisted tail); fast-forwarding it to archive head {head}");
                table.RaiseVirtualTail(head);
            }
        }

        private static void TruncateTablesAheadOfHead(IEnumerable<FreezerTable<byte[]>> tables, long head)
        {
            foreach (var table in tables.Where(table => !IsGenuinelyEmpty(table) && AbsoluteHead(table) > head))
                table.TruncateHead(head - table.VirtualTail);
        }

        private static void AlignTailGroups(Dictionary<string, FreezerTable<byte[]>> tables,
            Dictionary<string, FreezerTailGroup> tailGroupByName)
        {
            foreach (var group in tables.Keys.GroupBy(name => tailGroupByName[name]))
            {
                var members = group.Select(name => tables[name]).ToList();
                var newTail = members.Max(table => table.VirtualTail);

                foreach (var table in members.Where(table => table.VirtualTail < newTail))
                    table.RaiseVirtualTail(newTail);
            }
        }

        public long Items => ComputeHead(_tables.Values);

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.BeginBatch — begin a write batch")]
        public FreezerBatch BeginBatch() => new FreezerBatch(this);

        internal IReadOnlyDictionary<string, FreezerTable<byte[]>> TablesByName => _tables;

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.ReadCluster — read all five tables for a block")]
        public FrozenBlockCluster ReadCluster(long blockNumber)
        {
            return new FrozenBlockCluster(
                header: ReadItem("headers", blockNumber),
                hash: ReadItem("hashes", blockNumber),
                body: ReadItem("bodies", blockNumber),
                receipts: ReadItem("receipts", blockNumber),
                bal: ReadItem("bals", blockNumber));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.ReadHeader — narrow single-table read")]
        public byte[] ReadHeader(long blockNumber) => ReadItem("headers", blockNumber);
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.ReadHash — narrow single-table read")]
        public byte[] ReadHash(long blockNumber) => ReadItem("hashes", blockNumber);
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.ReadBody — narrow single-table read")]
        public byte[] ReadBody(long blockNumber) => ReadItem("bodies", blockNumber);
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.ReadReceipts — narrow single-table read")]
        public byte[] ReadReceipts(long blockNumber) => ReadItem("receipts", blockNumber);

        private byte[] ReadItem(string tableName, long blockNumber)
        {
            var table = _tables[tableName];
            return table.Read(blockNumber - table.VirtualTail);
        }

        public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedSegment(
            string tableName, ushort fileNumber, int? maxDegreeOfParallelism = null)
            => FreezerParallelSegment.DecodeSealed(_tables[tableName], fileNumber, maxDegreeOfParallelism);

        public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedRange(
            string tableName, long startBlock, int maxItems, int? maxDegreeOfParallelism = null)
        {
            var table = _tables[tableName];
            return FreezerParallelSegment.DecodeSealedRange(
                table, startBlock - table.VirtualTail, maxItems, maxDegreeOfParallelism);
        }

        public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedRange(
            string tableName, long startBlock, int maxItems, long endExclusiveBlock, int? maxDegreeOfParallelism = null)
        {
            var table = _tables[tableName];
            return FreezerParallelSegment.DecodeSealedRange(
                table, startBlock - table.VirtualTail, maxItems, endExclusiveBlock - table.VirtualTail, maxDegreeOfParallelism);
        }

        public IReadOnlyList<(long BlockNumber, byte[] Decoded)> DecodeSealedChunk(
            string tableName, long startBlock, int targetBytes, long endExclusiveBlock, int? maxDegreeOfParallelism = null)
        {
            var table = _tables[tableName];
            return FreezerParallelSegment.DecodeSealedChunk(
                table, startBlock - table.VirtualTail, targetBytes, endExclusiveBlock - table.VirtualTail, maxDegreeOfParallelism);
        }

        public ushort FileNumberOf(string tableName, long blockNumber)
        {
            var table = _tables[tableName];
            return table.FileNumberOf(blockNumber - table.VirtualTail);
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.SealedHead — sealed-file read boundary")]
        public long SealedHead(params string[] tableNames)
        {
            if (tableNames == null || tableNames.Length == 0)
                throw new ArgumentException("at least one table name required", nameof(tableNames));

            var min = long.MaxValue;
            foreach (var name in tableNames)
                min = Math.Min(min, _tables[name].SealedHead);
            return min;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.Encode — off-thread cluster encoding")]
        public EncodedFrozenCluster Encode(FrozenBlockCluster cluster) => new EncodedFrozenCluster(
            _tables["headers"].Encode(cluster.Header),
            _tables["hashes"].Encode(cluster.Hash),
            _tables["bodies"].Encode(cluster.Body),
            _tables["receipts"].Encode(cluster.Receipts),
            _tables["bals"].Encode(cluster.Bal));

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.CommittedHead — durable fsynced read boundary")]
        public long CommittedHead(params string[] tableNames)
        {
            if (tableNames == null || tableNames.Length == 0)
                throw new ArgumentException("at least one table name required", nameof(tableNames));

            var min = long.MaxValue;
            foreach (var name in tableNames)
                min = Math.Min(min, _tables[name].DurableHead);
            return min;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "Freezer.TruncateHead — deep-reorg rollback")]
        public void TruncateHead(long newItemCount)
        {
            var highestTail = _tables.Values.Max(table => table.VirtualTail);
            if (newItemCount < highestTail)
                throw new FreezerImmutableException(
                    $"cannot truncate freezer to {newItemCount}: below the highest tail {highestTail}");

            if (newItemCount > Items)
                return;

            foreach (var table in _tables.Values)
                table.TruncateHead(newItemCount - table.VirtualTail);
        }

        public IReadOnlyDictionary<string, TableTail> Tails =>
            _tables.Keys
                .GroupBy(name => _tailGroupByName[name])
                .ToDictionary(GroupLabel, BuildTail);

        private static string GroupLabel(IGrouping<FreezerTailGroup, string> group) =>
            string.Join("+", group.OrderBy(name => name, StringComparer.Ordinal));

        private TableTail BuildTail(IGrouping<FreezerTailGroup, string> group)
        {
            var members = group.ToList();
            var virtualTail = members.Max(name => _tables[name].VirtualTail);
            return new TableTail(virtualTail, members);
        }

        public void Dispose() => DisposeAll(_tables.Values);
    }
}
