using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Documentation;

namespace Nethereum.Freezer
{
    public sealed class FreezerBatch : IDisposable
    {
        private readonly Freezer _freezer;
        private Dictionary<string, long> _baselineCounts;

        internal FreezerBatch(Freezer freezer)
        {
            _freezer = freezer;
            _baselineCounts = SnapshotCounts();
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerBatch.AppendCluster — append one lockstep block")]
        public void AppendCluster(long blockNumber, FrozenBlockCluster cluster)
        {
            RequireEveryTableAtBlockNumber(blockNumber);

            AppendItem("headers", blockNumber, cluster.Header);
            AppendItem("hashes", blockNumber, cluster.Hash);
            AppendItem("bodies", blockNumber, cluster.Body);
            AppendItem("receipts", blockNumber, cluster.Receipts);
            AppendItem("bals", blockNumber, cluster.Bal);
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerBatch.AppendEncodedCluster — append a pre-encoded block")]
        public void AppendEncodedCluster(long blockNumber, EncodedFrozenCluster cluster)
        {
            RequireEveryTableAtBlockNumber(blockNumber);

            AppendEncodedItem("headers", blockNumber, cluster.Header);
            AppendEncodedItem("hashes", blockNumber, cluster.Hash);
            AppendEncodedItem("bodies", blockNumber, cluster.Body);
            AppendEncodedItem("receipts", blockNumber, cluster.Receipts);
            AppendEncodedItem("bals", blockNumber, cluster.Bal);
        }

        private void AppendEncodedItem(string tableName, long blockNumber, byte[] encoded)
        {
            var table = _freezer.TablesByName[tableName];
            table.AppendEncoded(blockNumber - table.VirtualTail, encoded);
        }

        private void RequireEveryTableAtBlockNumber(long blockNumber)
        {
            foreach (var table in _freezer.TablesByName.Values)
            {
                var expected = table.VirtualTail + table.Count;
                if (expected != blockNumber)
                    throw new FreezerConsistencyException(
                        $"freezer table '{table.Name}' expected next block {expected}, got {blockNumber}");
            }
        }

        private void AppendItem(string tableName, long blockNumber, byte[] item)
        {
            var table = _freezer.TablesByName[tableName];
            table.Append(blockNumber - table.VirtualTail, item);
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerBatch.Commit — fsync durability boundary")]
        public long Commit()
        {
            AssertTablesAgree();

            foreach (var table in _freezer.TablesByName.Values)
                table.SyncIndex();

            foreach (var table in _freezer.TablesByName.Values)
                table.SyncData();

            foreach (var table in _freezer.TablesByName.Values)
                table.PersistMeta();

            _baselineCounts = SnapshotCounts();
            return _freezer.Items;
        }

        private void AssertTablesAgree()
        {
            var heights = _freezer.TablesByName.Values
                .Select(table => table.VirtualTail + table.Count)
                .Distinct()
                .ToList();

            if (heights.Count > 1)
                throw new FreezerConsistencyException(
                    $"freezer tables disagree before commit: heights {string.Join(",", heights)}");
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FreezerBatch.Reset — discard staged appends")]
        public void Reset()
        {
            foreach (var (name, table) in _freezer.TablesByName)
                table.TruncateHead(_baselineCounts[name]);
        }

        private Dictionary<string, long> SnapshotCounts() =>
            _freezer.TablesByName.ToDictionary(pair => pair.Key, pair => pair.Value.Count);

        public void Dispose()
        {
        }
    }
}
