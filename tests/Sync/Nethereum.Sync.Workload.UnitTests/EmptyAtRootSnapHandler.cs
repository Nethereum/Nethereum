using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class EmptyAtRootSnapHandler : ISnapRequestHandler
    {
        private readonly ISnapRequestHandler _inner;
        private readonly byte[] _emptyRoot;
        private readonly ConcurrentDictionary<string, int> _emptyReplies = new();

        public EmptyAtRootSnapHandler(ISnapRequestHandler inner, byte[] emptyRoot)
        {
            _inner = inner;
            _emptyRoot = emptyRoot;
        }

        public int EmptyReplies(string kind) => _emptyReplies.TryGetValue(kind, out var n) ? n : 0;

        public int TotalEmptyReplies
        {
            get
            {
                var total = 0;
                foreach (var n in _emptyReplies.Values) total += n;
                return total;
            }
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            => AnswersEmpty(nameof(GetAccountRangeAsync), request.RootHash)
                ? Task.FromResult(new AccountRangeMessage { RequestId = request.RequestId })
                : _inner.GetAccountRangeAsync(request, ct);

        public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            => AnswersEmpty(nameof(GetStorageRangesAsync), request.RootHash)
                ? Task.FromResult(new StorageRangesMessage { RequestId = request.RequestId, Slots = new List<List<StorageRangesMessage.SlotEntry>>(), Proof = new List<byte[]>() })
                : _inner.GetStorageRangesAsync(request, ct);

        public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
            => AnswersEmpty(nameof(GetTrieNodesAsync), request.RootHash)
                ? Task.FromResult(new TrieNodesMessage { RequestId = request.RequestId, Nodes = new List<byte[]>() })
                : _inner.GetTrieNodesAsync(request, ct);

        public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
            => _inner.GetByteCodesAsync(request, ct);

        public Task<BlockAccessListsMessage> GetBlockAccessListsAsync(GetBlockAccessListsMessage request, CancellationToken ct = default)
            => _inner.GetBlockAccessListsAsync(request, ct);

        private bool AnswersEmpty(string kind, byte[] root)
        {
            if (!ByteUtil.AreEqual(root, _emptyRoot)) return false;
            _emptyReplies.AddOrUpdate(kind, 1, (_, n) => n + 1);
            return true;
        }
    }
}
