namespace Nethereum.RPC.DebugNode
{
    public interface IDebugApiService
    {
        IDebugGetBadBlocks GetBadBlocks { get; }
        IDebugGetRawBlock GetRawBlock { get; }
        IDebugGetRawHeader GetRawHeader { get; }
        IDebugGetRawReceipts GetRawReceipts { get; }
        IDebugGetRawTransaction GetRawTransaction { get; }
        IDebugStorageRangeAt StorageRangeAt { get; }
    }
}
