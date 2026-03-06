using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IHubMessageService
    {
        Task<MessageInfo?> GetMessageAsync(ulong messageId);
        Task<List<MessageInfo>> GetMessageRangeAsync(ulong fromId, ulong toId);
        Task<ulong> GetPendingMessageCountAsync();
        Task<List<MessageInfo>> GetPendingMessagesAsync(ulong lastProcessedId, int maxMessages);
    }
}
