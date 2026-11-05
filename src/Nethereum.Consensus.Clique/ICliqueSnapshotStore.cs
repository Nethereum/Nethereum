using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Consensus.Clique
{
    [NethereumDocExample(DocSection.Consensus, "clique", "Persist a Clique snapshot every SnapshotInterval blocks")]
    public interface ICliqueSnapshotStore
    {
        void Save(CliqueSnapshot snapshot);
        CliqueSnapshot? Load(byte[] blockHash);
    }

    public sealed class InMemoryCliqueSnapshotStore : ICliqueSnapshotStore
    {
        private readonly Dictionary<string, byte[]> _encoded = new();
        private readonly object _lock = new();

        public void Save(CliqueSnapshot snapshot)
        {
            var encoded = CliqueSnapshotCodec.Encode(snapshot);
            lock (_lock)
            {
                _encoded[snapshot.BlockHash.ToHex()] = encoded;
            }
        }

        public CliqueSnapshot? Load(byte[] blockHash)
        {
            lock (_lock)
            {
                return _encoded.TryGetValue(blockHash.ToHex(), out var encoded)
                    ? CliqueSnapshotCodec.Decode(encoded)
                    : null;
            }
        }
    }
}
