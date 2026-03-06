using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessageProcessor
    {
        Task<MessageBatchResult> ProcessBatchAsync(IReadOnlyList<MessageInfo> messages);
        IMessageMerkleAccumulator Accumulator { get; }
        IMessageResultStore? ResultStore { get; }
    }
}
