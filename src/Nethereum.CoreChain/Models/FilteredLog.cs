using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Models
{
    public class FilteredLog
    {
        public string Address { get; set; }
        public byte[] Data { get; set; }
        public List<byte[]> Topics { get; set; }
        public byte[] BlockHash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public byte[] TransactionHash { get; set; }
        public int TransactionIndex { get; set; }
        public int LogIndex { get; set; }
        public bool Removed { get; set; }

        public FilteredLog()
        {
            Topics = new List<byte[]>();
        }

        public static FilteredLog FromLog(Log log, byte[] blockHash, BigInteger blockNumber, byte[] txHash, int txIndex, int logIndex)
        {
            return new FilteredLog
            {
                Address = log.Address,
                Data = log.Data,
                Topics = log.Topics ?? new List<byte[]>(),
                BlockHash = blockHash,
                BlockNumber = blockNumber,
                TransactionHash = txHash,
                TransactionIndex = txIndex,
                LogIndex = logIndex,
                Removed = false
            };
        }

        public Log ToLog()
        {
            return new Log
            {
                Address = Address,
                Data = Data,
                Topics = Topics ?? new List<byte[]>()
            };
        }
    }
}
