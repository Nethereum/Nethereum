namespace Nethereum.CoreChain.Storage
{
    public interface ISnapSyncStateEncoder
    {
        byte[] Encode(SnapSyncState state);
        SnapSyncState Decode(byte[] data);
    }
}
