using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.Explorer.Services;

internal static class DirectionFilterHelper
{
    internal static IQueryable<TransactionBase> ApplyDirectionFilter(
        IQueryable<TransactionBase> query, string normalizedAddress, string? direction)
    {
        return direction switch
        {
            "in" => query.Where(t => t.AddressTo == normalizedAddress && t.AddressFrom != normalizedAddress),
            "out" => query.Where(t => t.AddressFrom == normalizedAddress && t.AddressTo != normalizedAddress),
            "self" => query.Where(t => t.AddressFrom == normalizedAddress && t.AddressTo == normalizedAddress),
            _ => query.Where(t => t.AddressFrom == normalizedAddress || t.AddressTo == normalizedAddress)
        };
    }

    internal static IQueryable<InternalTransaction> ApplyDirectionFilter(
        IQueryable<InternalTransaction> query, string normalizedAddress, string? direction)
    {
        return direction switch
        {
            "in" => query.Where(t => t.AddressTo == normalizedAddress && t.AddressFrom != normalizedAddress),
            "out" => query.Where(t => t.AddressFrom == normalizedAddress && t.AddressTo != normalizedAddress),
            "self" => query.Where(t => t.AddressFrom == normalizedAddress && t.AddressTo == normalizedAddress),
            _ => query.Where(t => t.AddressFrom == normalizedAddress || t.AddressTo == normalizedAddress)
        };
    }
}
