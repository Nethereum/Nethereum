using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class InMemoryMessageIndexStore : IMessageIndexStore
    {
        private readonly ConcurrentDictionary<ulong, SortedDictionary<ulong, MessageInfo>> _chains = new();
        private readonly ConcurrentDictionary<ulong, object> _locks = new();
        private readonly ConcurrentDictionary<ulong, InMemoryBlockchainProgressRepository> _progressRepos = new();

        public Task StoreAsync(MessageInfo message)
        {
            var dict = GetOrCreateChainDict(message.SourceChainId);
            var lockObj = GetLock(message.SourceChainId);
            lock (lockObj)
            {
                dict[message.MessageId] = message;
            }
            return Task.CompletedTask;
        }

        public Task StoreBatchAsync(IEnumerable<MessageInfo> messages)
        {
            foreach (var group in messages.GroupBy(m => m.SourceChainId))
            {
                var dict = GetOrCreateChainDict(group.Key);
                var lockObj = GetLock(group.Key);
                lock (lockObj)
                {
                    foreach (var message in group)
                    {
                        dict[message.MessageId] = message;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task<MessageInfo?> GetAsync(ulong sourceChainId, ulong messageId)
        {
            var dict = GetOrCreateChainDict(sourceChainId);
            var lockObj = GetLock(sourceChainId);
            lock (lockObj)
            {
                dict.TryGetValue(messageId, out var message);
                return Task.FromResult(message);
            }
        }

        public Task<List<MessageInfo>> GetPendingAsync(ulong sourceChainId, ulong afterMessageId, int maxCount)
        {
            var dict = GetOrCreateChainDict(sourceChainId);
            var lockObj = GetLock(sourceChainId);
            lock (lockObj)
            {
                var result = dict
                    .Where(kvp => kvp.Key > afterMessageId)
                    .Take(maxCount)
                    .Select(kvp => kvp.Value)
                    .ToList();
                return Task.FromResult(result);
            }
        }

        public Task<ulong> GetLastIndexedMessageIdAsync(ulong sourceChainId)
        {
            var dict = GetOrCreateChainDict(sourceChainId);
            var lockObj = GetLock(sourceChainId);
            lock (lockObj)
            {
                var lastId = dict.Count > 0 ? dict.Keys.Max() : 0UL;
                return Task.FromResult(lastId);
            }
        }

        public Task RemoveFromAsync(ulong sourceChainId, ulong messageId)
        {
            var dict = GetOrCreateChainDict(sourceChainId);
            var lockObj = GetLock(sourceChainId);
            lock (lockObj)
            {
                var keysToRemove = dict.Keys.Where(k => k >= messageId).ToList();
                foreach (var key in keysToRemove)
                {
                    dict.Remove(key);
                }
            }
            return Task.CompletedTask;
        }

        public IBlockProgressRepository GetBlockProgressRepository(ulong sourceChainId)
        {
            return _progressRepos.GetOrAdd(sourceChainId, _ => new InMemoryBlockchainProgressRepository());
        }

        private SortedDictionary<ulong, MessageInfo> GetOrCreateChainDict(ulong sourceChainId)
        {
            return _chains.GetOrAdd(sourceChainId, _ => new SortedDictionary<ulong, MessageInfo>());
        }

        private object GetLock(ulong sourceChainId)
        {
            return _locks.GetOrAdd(sourceChainId, _ => new object());
        }
    }
}
