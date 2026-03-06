using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessageResultStore
    {
        Task StoreAsync(MessageResult result);
        Task<MessageResult?> GetByMessageIdAsync(ulong sourceChainId, ulong messageId);
        Task<IReadOnlyList<MessageResult>> GetAllBySourceChainOrderedByLeafIndexAsync(ulong sourceChainId);
        Task<IReadOnlyList<MessageResult>> GetBySourceChainAsync(ulong sourceChainId, int offset, int count);
        Task<IReadOnlyList<ulong>> GetSourceChainIdsAsync();
        Task<int> GetCountAsync(ulong sourceChainId);
    }
}
