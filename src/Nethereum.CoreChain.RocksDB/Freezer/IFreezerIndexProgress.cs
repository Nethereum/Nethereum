namespace Nethereum.CoreChain.RocksDB.Freezer
{
    public interface IFreezerIndexProgress
    {
        ulong GetByHashCursor();
        void SetByHashCursor(ulong itemNumber);
        (long LvLowerBound, long CountedItems) GetFilterMapsLvProgress();
        void SetFilterMapsLvProgress(long lvLowerBound, long countedItems);
    }
}
