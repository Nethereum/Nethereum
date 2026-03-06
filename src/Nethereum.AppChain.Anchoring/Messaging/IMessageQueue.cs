using System.Collections.Generic;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessageQueue
    {
        void Enqueue(MessageInfo message);
        void EnqueueRange(IEnumerable<MessageInfo> messages);
        List<MessageInfo> DrainBatch(int maxBatchSize);
        int Count { get; }
    }
}
