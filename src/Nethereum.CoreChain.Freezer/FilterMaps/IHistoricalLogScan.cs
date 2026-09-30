using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public interface IHistoricalLogScan
    {
        Task<IReadOnlyList<ResolvedLog>> ScanAsync(LogFilter filter, long fromBlock, long toBlock);
    }
}
