using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.Model.P2P.Snap;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class ScriptedSnapPeer : ISnapPeer
    {
        private readonly ISnapRequestHandler _handler;
        private readonly object _gate = new();
        private readonly List<(int AfterRequest, Action Fire)> _scripted = new();
        private int _requests;
        private int _trieNodeRequests;

        public ScriptedSnapPeer(ISnapRequestHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public int Requests => Volatile.Read(ref _requests);

        public int TrieNodeRequests => Volatile.Read(ref _trieNodeRequests);

        public void FireAfterRequest(int requestNumber, Action fire)
        {
            lock (_gate) _scripted.Add((requestNumber, fire));
        }

        public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage request, CancellationToken ct = default)
            => ServeAsync(() => _handler.GetAccountRangeAsync(request, ct));

        public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage request, CancellationToken ct = default)
            => ServeAsync(() => _handler.GetStorageRangesAsync(request, ct));

        public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage request, CancellationToken ct = default)
            => ServeAsync(() => _handler.GetByteCodesAsync(request, ct));

        public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _trieNodeRequests);
            return ServeAsync(() => _handler.GetTrieNodesAsync(request, ct));
        }

        private async Task<T> ServeAsync<T>(Func<Task<T>> serve)
        {
            var response = await serve().ConfigureAwait(false);
            var served = Interlocked.Increment(ref _requests);
            foreach (var fire in TakeDue(served)) fire();
            return response;
        }

        private List<Action> TakeDue(int served)
        {
            lock (_gate)
            {
                var due = _scripted.Where(s => s.AfterRequest == served).Select(s => s.Fire).ToList();
                _scripted.RemoveAll(s => s.AfterRequest == served);
                return due;
            }
        }
    }
}
