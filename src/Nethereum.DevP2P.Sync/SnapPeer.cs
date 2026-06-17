using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Abstraction over a snap/1 peer connection. The real implementation
    /// wraps an RlpxConnection with snap capability negotiated; an in-memory
    /// implementation wraps an ISnapRequestHandler directly for tests.
    /// Both produce typed responses in identical shape.
    /// </summary>
    public interface ISnapPeer
    {
        Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default);
        Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default);
        Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default);
        Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default);
    }

    /// <summary>
    /// In-process ISnapPeer that delegates straight to a local
    /// ISnapRequestHandler. Used in tests and for self-sync scenarios.
    /// </summary>
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
