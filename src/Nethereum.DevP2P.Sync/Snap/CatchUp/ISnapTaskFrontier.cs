namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public interface ISnapTaskFrontier
    {
        bool IsAccountFetched(byte[] accountHash);

        bool IsStorageFetched(byte[] accountHash, byte[] slotHash);
    }
}
