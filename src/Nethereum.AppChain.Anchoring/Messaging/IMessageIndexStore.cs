using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessageIndexStore
    {
        Task StoreAsync(MessageInfo message);
        Task StoreBatchAsync(IEnumerable<MessageInfo> messages);
        Task<MessageInfo?> GetAsync(ulong sourceChainId, ulong messageId);
        Task<List<MessageInfo>> GetPendingAsync(ulong sourceChainId, ulong afterMessageId, int maxCount);
        Task<ulong> GetLastIndexedMessageIdAsync(ulong sourceChainId);
        Task RemoveFromAsync(ulong sourceChainId, ulong messageId);
        IBlockProgressRepository GetBlockProgressRepository(ulong sourceChainId);
    }
}
