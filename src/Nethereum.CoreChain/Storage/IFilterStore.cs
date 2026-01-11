using System;
using System.Numerics;
using Nethereum.CoreChain.Models;

namespace Nethereum.CoreChain.Storage
{
    public interface IFilterStore
    {
        string CreateLogFilter(LogFilter filter, BigInteger currentBlock);
        string CreateBlockFilter(BigInteger currentBlock);
        string CreatePendingTransactionFilter();
        FilterState GetFilter(string filterId);
        bool RemoveFilter(string filterId);
        void UpdateFilterLastBlock(string filterId, BigInteger blockNumber);
    }

    public class FilterState
    {
        public string Id { get; set; }
        public FilterType Type { get; set; }
        public LogFilter LogFilter { get; set; }
        public BigInteger LastCheckedBlock { get; set; }
        public DateTime CreatedAt { get; set; }

        public FilterState()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }

    public enum FilterType
    {
        Log,
        Block,
        PendingTransaction
    }
}
