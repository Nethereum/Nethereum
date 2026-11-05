using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface ISnapPeer
    {
        Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default);
        Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default);
        Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default);
        Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default);

        async Task<AccountRangeMessage> GetAccountRangeAsync(
            GetAccountRangeMessage request, Func<AccountRangeMessage, bool> verifyResponse, CancellationToken ct = default)
        {
            var reply = await GetAccountRangeAsync(request, ct).ConfigureAwait(false);
            if (verifyResponse != null && !verifyResponse(reply))
                throw new Nethereum.DevP2P.Sync.Scheduling.FetchRequestFailedException("account range reply failed verification", null);
            return reply;
        }

        async Task<StorageRangesMessage> GetStorageRangesAsync(
            GetStorageRangesMessage request, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct = default)
        {
            var reply = await GetStorageRangesAsync(request, ct).ConfigureAwait(false);
            if (verifyResponse != null && !verifyResponse(reply))
                throw new Nethereum.DevP2P.Sync.Scheduling.FetchRequestFailedException("storage range reply failed verification", null);
            return reply;
        }

        async Task<ByteCodesMessage> GetByteCodesAsync(
            GetByteCodesMessage request, Func<ByteCodesMessage, bool> verifyResponse, CancellationToken ct = default)
        {
            var reply = await GetByteCodesAsync(request, ct).ConfigureAwait(false);
            if (verifyResponse != null && !verifyResponse(reply))
                throw new Nethereum.DevP2P.Sync.Scheduling.FetchRequestFailedException("bytecode reply failed verification", null);
            return reply;
        }
    }

    public class InProcessSnapPeer : ISnapPeer
    {
        private readonly ISnapRequestHandler _handler;
        public InProcessSnapPeer(ISnapRequestHandler handler) { _handler = handler; }

        public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            => _handler.GetAccountRangeAsync(r, ct);
        public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            => _handler.GetStorageRangesAsync(r, ct);
        public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
            => _handler.GetByteCodesAsync(r, ct);
        public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
            => _handler.GetTrieNodesAsync(r, ct);
    }
}
