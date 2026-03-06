using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageQueue : IMessageQueue
    {
        private readonly ConcurrentQueue<MessageInfo> _queue = new();

        public void Enqueue(MessageInfo message)
        {
            _queue.Enqueue(message);
        }

        public void EnqueueRange(IEnumerable<MessageInfo> messages)
        {
            foreach (var message in messages)
            {
                _queue.Enqueue(message);
            }
        }

        public List<MessageInfo> DrainBatch(int maxBatchSize)
        {
            var batch = new List<MessageInfo>(maxBatchSize);
            while (batch.Count < maxBatchSize && _queue.TryDequeue(out var message))
            {
                batch.Add(message);
            }
            return batch
                .OrderBy(m => m.SourceChainId)
                .ThenBy(m => m.MessageId)
                .ToList();
        }

        public int Count => _queue.Count;
    }
}
