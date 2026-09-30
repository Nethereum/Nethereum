namespace Nethereum.Freezer.FilterMaps
{
    public interface IFilterMapsQueryBackend
    {
        FilterMapsParams Params { get; }

        long GetBlockLvPointer(long blockNumber);

        FilterRow GetFilterMapRow(long mapIndex, int rowIndex);
    }
}
