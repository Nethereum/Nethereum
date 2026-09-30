namespace Nethereum.Freezer.FilterMaps
{
    public interface IFilterMapsStore
    {
        FilterMapsRange ReadRange();
        void WriteRange(FilterMapsRange range);

        byte[] ReadBaseRowGroup(long mapRowIndex);
        void WriteBaseRowGroup(long mapRowIndex, byte[] value);

        byte[] ReadExtRow(long mapRowIndex);
        void WriteExtRow(long mapRowIndex, byte[] value);

        (long blockNumber, byte[] blockId)? ReadLastBlockOfMap(long mapIndex);
        void WriteLastBlockOfMap(long mapIndex, long blockNumber, byte[] blockId);

        long? ReadBlockLvPointer(long blockNumber);
        void WriteBlockLvPointer(long blockNumber, long lvPointer);
    }
}
