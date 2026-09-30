using System.Collections.Generic;
using System.Numerics;
using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class DerivedLog
    {
        public Log Log { get; }
        public byte[] BlockHash { get; }
        public long BlockNumber { get; }
        public byte[] TxHash { get; }
        public int TxIndex { get; }

        public int LogIndex { get; }

        public DerivedLog(Log log, byte[] blockHash, long blockNumber, byte[] txHash, int txIndex, int logIndex)
        {
            Log = log;
            BlockHash = blockHash;
            BlockNumber = blockNumber;
            TxHash = txHash;
            TxIndex = txIndex;
            LogIndex = logIndex;
        }
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "DerivedReceipt — the full receipt reconstructed on read")]
    public sealed class DerivedReceipt
    {
        public byte[] PostStateOrStatus { get; }
        public BigInteger CumulativeGasUsed { get; }
        public BigInteger GasUsed { get; }
        public byte[] Bloom { get; }
        public IReadOnlyList<DerivedLog> Logs { get; }
        public byte[] TxHash { get; }
        public TransactionType TransactionType { get; }
        public EvmUInt256 EffectiveGasPrice { get; }

        public string ContractAddress { get; }

        public BigInteger? BlobGasUsed { get; }
        public EvmUInt256? BlobGasPrice { get; }

        public DerivedReceipt(
            byte[] postStateOrStatus,
            BigInteger cumulativeGasUsed,
            BigInteger gasUsed,
            byte[] bloom,
            IReadOnlyList<DerivedLog> logs,
            byte[] txHash,
            TransactionType transactionType,
            EvmUInt256 effectiveGasPrice,
            string contractAddress,
            BigInteger? blobGasUsed,
            EvmUInt256? blobGasPrice)
        {
            PostStateOrStatus = postStateOrStatus;
            CumulativeGasUsed = cumulativeGasUsed;
            GasUsed = gasUsed;
            Bloom = bloom;
            Logs = logs;
            TxHash = txHash;
            TransactionType = transactionType;
            EffectiveGasPrice = effectiveGasPrice;
            ContractAddress = contractAddress;
            BlobGasUsed = blobGasUsed;
            BlobGasPrice = blobGasPrice;
        }
    }
}
