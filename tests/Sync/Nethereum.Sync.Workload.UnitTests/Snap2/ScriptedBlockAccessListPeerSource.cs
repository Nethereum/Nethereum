using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Snap.CatchUp;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class ScriptedBlockAccessListPeerSource : IBlockAccessListPeerSource
    {
        private readonly ServingPeer _peer;

        public ScriptedBlockAccessListPeerSource(Func<byte[], byte[]> accessListByBlockHash)
        {
            _peer = new ServingPeer(accessListByBlockHash ?? throw new ArgumentNullException(nameof(accessListByBlockHash)));
        }

        public IReadOnlyList<byte[]> RequestedBlockHashes => _peer.Requested;

        public IReadOnlyList<IBlockAccessListPeer> GetServiceablePeers() => new IBlockAccessListPeer[] { _peer };

        private sealed class ServingPeer : IBlockAccessListPeer
        {
            private readonly Func<byte[], byte[]> _accessListByBlockHash;
            private readonly List<byte[]> _requested = new();

            public ServingPeer(Func<byte[], byte[]> accessListByBlockHash)
            {
                _accessListByBlockHash = accessListByBlockHash;
            }

            public string Id => "scripted-bal-peer";

            public IReadOnlyList<byte[]> Requested
            {
                get { lock (_requested) return _requested.ToList(); }
            }

            public Task<IReadOnlyList<byte[]>> RequestBlockAccessListsAsync(
                IReadOnlyList<byte[]> blockHashes, ulong responseBytes, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                lock (_requested) _requested.AddRange(blockHashes);
                IReadOnlyList<byte[]> served = blockHashes
                    .Select(hash => _accessListByBlockHash(hash) ?? Array.Empty<byte>())
                    .ToList();
                return Task.FromResult(served);
            }
        }
    }
}
