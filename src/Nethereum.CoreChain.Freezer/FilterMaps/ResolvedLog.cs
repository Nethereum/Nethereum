using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public sealed class ResolvedLog
    {
        public long BlockNumber { get; }
        public int TransactionIndex { get; }

        public int LogIndex { get; }

        public Log Log { get; }

        public ResolvedLog(long blockNumber, int transactionIndex, int logIndex, Log log)
        {
            BlockNumber = blockNumber;
            TransactionIndex = transactionIndex;
            LogIndex = logIndex;
            Log = log;
        }
    }
}
