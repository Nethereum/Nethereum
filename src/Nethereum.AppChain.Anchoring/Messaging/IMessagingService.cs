using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IMessagingService
    {
        Task<List<MessageInfo>> PollAllSourcesAsync();
        ulong GetLastProcessedMessageId(ulong sourceChainId);
        event Func<MessageInfo, Task>? OnMessageReceived;
    }
}
