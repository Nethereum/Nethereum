using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class InMemoryMessageResultStore : IMessageResultStore
    {
        private readonly ConcurrentDictionary<(ulong sourceChainId, ulong messageId), MessageResult> _byMessageId = new();
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<int, MessageResult>> _byLeafIndex = new();

        public Task StoreAsync(MessageResult result)
        {
            _byMessageId[(result.SourceChainId, result.MessageId)] = result;

            var chainLeaves = _byLeafIndex.GetOrAdd(result.SourceChainId, _ => new ConcurrentDictionary<int, MessageResult>());
            chainLeaves[result.LeafIndex] = result;

            return Task.CompletedTask;
        }

        public Task<MessageResult?> GetByMessageIdAsync(ulong sourceChainId, ulong messageId)
        {
            _byMessageId.TryGetValue((sourceChainId, messageId), out var result);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<MessageResult>> GetAllBySourceChainOrderedByLeafIndexAsync(ulong sourceChainId)
        {
            if (!_byLeafIndex.TryGetValue(sourceChainId, out var chainLeaves))
                return Task.FromResult<IReadOnlyList<MessageResult>>(new List<MessageResult>());

            var ordered = chainLeaves.Values.OrderBy(r => r.LeafIndex).ToList();
            return Task.FromResult<IReadOnlyList<MessageResult>>(ordered);
        }

        public Task<IReadOnlyList<MessageResult>> GetBySourceChainAsync(ulong sourceChainId, int offset, int count)
        {
            if (!_byLeafIndex.TryGetValue(sourceChainId, out var chainLeaves))
                return Task.FromResult<IReadOnlyList<MessageResult>>(new List<MessageResult>());

            var results = chainLeaves.Values
                .OrderBy(r => r.LeafIndex)
                .Skip(offset)
                .Take(count)
                .ToList();
            return Task.FromResult<IReadOnlyList<MessageResult>>(results);
        }

        public Task<IReadOnlyList<ulong>> GetSourceChainIdsAsync()
        {
            return Task.FromResult<IReadOnlyList<ulong>>(_byLeafIndex.Keys.ToList());
        }

        public Task<int> GetCountAsync(ulong sourceChainId)
        {
            if (_byLeafIndex.TryGetValue(sourceChainId, out var chainLeaves))
                return Task.FromResult(chainLeaves.Count);
            return Task.FromResult(0);
        }
    }
}
