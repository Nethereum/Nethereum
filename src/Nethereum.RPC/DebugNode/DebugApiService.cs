using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.DebugNode
{
    public class DebugApiService : RpcClientWrapper, IDebugApiService
    {
        public DebugApiService(IClient client) : base(client)
        {
            GetBadBlocks = new DebugGetBadBlocks(client);
            GetRawBlock = new DebugGetRawBlock(client);
            GetRawHeader = new DebugGetRawHeader(client);
            GetRawReceipts = new DebugGetRawReceipts(client);
            GetRawTransaction = new DebugGetRawTransaction(client);
            StorageRangeAt  = new DebugStorageRangeAt(client);
        }

        public IDebugGetBadBlocks GetBadBlocks { get; private set; }

        public IDebugGetRawBlock GetRawBlock { get; private set; }

        public IDebugGetRawHeader GetRawHeader { get; private set; }

        public IDebugGetRawReceipts GetRawReceipts { get; private set; }

        public IDebugGetRawTransaction GetRawTransaction { get; private set; }

        public IDebugStorageRangeAt StorageRangeAt { get; private set; }
    }
}