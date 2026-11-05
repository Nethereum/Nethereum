using System;
using System.Text;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain.RocksDB;

namespace Nethereum.AppChain.Server.Hosting
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ChainInfrastructure, "appchain-server-core", "RocksDbCliqueSnapshotStore")]
    public sealed class RocksDbCliqueSnapshotStore : ICliqueSnapshotStore
    {
        private static readonly byte[] KeyPrefix = Encoding.ASCII.GetBytes("clique-snapshot:");

        private readonly RocksDbManager _rocks;

        public RocksDbCliqueSnapshotStore(RocksDbManager rocks)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
        }

        public void Save(CliqueSnapshot snapshot)
        {
            _rocks.Put(RocksDbManager.CF_METADATA, KeyFor(snapshot.BlockHash), CliqueSnapshotCodec.Encode(snapshot));
        }

        public CliqueSnapshot? Load(byte[] blockHash)
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, KeyFor(blockHash));
            return raw == null ? null : CliqueSnapshotCodec.Decode(raw);
        }

        private static byte[] KeyFor(byte[] blockHash)
        {
            var key = new byte[KeyPrefix.Length + blockHash.Length];
            Buffer.BlockCopy(KeyPrefix, 0, key, 0, KeyPrefix.Length);
            Buffer.BlockCopy(blockHash, 0, key, KeyPrefix.Length, blockHash.Length);
            return key;
        }
    }
}
