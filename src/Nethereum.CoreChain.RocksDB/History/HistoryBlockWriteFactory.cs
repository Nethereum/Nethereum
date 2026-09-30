using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.History
{
    public static class HistoryBlockWriteFactory
    {
        public static HistoryBlockWrite FromPersistable(
            PersistableBlock pb, IBlockEncodingProvider provider, RocksDbSerializer serializer, bool enableLogIndex = true)
        {
            if (pb == null) throw new ArgumentNullException(nameof(pb));
            var blockNumber = (ulong)(long)pb.Header.BlockNumber.ToBigInteger();
            BigInteger blockNumberBig = pb.Header.BlockNumber.ToBigInteger();

            var txs = pb.Transactions;
            var receipts = pb.Receipts;
            var txWrites = new List<HistoryTxWrite>(txs?.Count ?? 0);
            for (int i = 0; i < (txs?.Count ?? 0); i++)
            {
                var tx = txs[i];
                var w = new HistoryTxWrite { Tx = provider.EncodeTransaction(tx) };
                if (receipts != null && i < receipts.Count)
                {
                    var it = receipts[i];
                    w.TxHash = it.TxHash ?? tx.Hash;
                    w.Receipt = serializer.SerializeReceiptInfoWith(new ReceiptInfo
                    {
                        Receipt = it.Receipt,
                        TxHash = it.TxHash,
                        BlockHash = pb.Hash,
                        BlockNumber = blockNumberBig,
                        TransactionIndex = it.TxIndex,
                        GasUsed = it.GasUsed,
                        ContractAddress = it.ContractAddress,
                        EffectiveGasPrice = it.EffectiveGasPrice,
                    });
                }
                else
                {
                    w.TxHash = tx.Hash;
                }
                txWrites.Add(w);
            }

            var meta = new BlockMeta
            {
                BlockHash = pb.Hash,
                TxCount = txs?.Count ?? 0,
                Uncles = EncodeUncles(pb.Uncles, provider),
                Withdrawals = EncodeWithdrawals(pb.Withdrawals),
                Bloom = pb.Bloom ?? Array.Empty<byte>(),
            };

            List<(string Cf, byte[] Key, byte[] Value)> seqExtra = null, indexExtra = null;
            if (enableLogIndex)
            {
                if (pb.Logs != null && pb.Logs.Count > 0)
                {
                    seqExtra = new List<(string, byte[], byte[])>();
                    indexExtra = new List<(string, byte[], byte[])>();
                    Stores.RocksDbLogStore.CollectBulkWrites(pb.Logs, pb.Hash, blockNumberBig, seqExtra, indexExtra);
                }
                if (pb.Bloom != null && pb.Bloom.Length == 256)
                {
                    seqExtra ??= new List<(string, byte[], byte[])>();
                    seqExtra.Add((RocksDbManager.CF_BLOCK_BLOOMS, Stores.RocksDbLogStore.CreateBlockNumberKey(blockNumberBig), pb.Bloom));
                }
            }

            return new HistoryBlockWrite
            {
                BlockNumber = blockNumber,
                BlockHash = pb.Hash,
                Header = provider.EncodeBlockHeader(pb.Header),
                Meta = BlockMetaCodec.Encode(meta),
                Transactions = txWrites,
                SequentialExtra = seqExtra,
                IndexExtra = indexExtra,
            };
        }

        private static byte[] EncodeUncles(IList<BlockHeader> uncles, IBlockEncodingProvider provider)
        {
            var n = uncles?.Count ?? 0;
            var encoded = new byte[n][];
            for (int i = 0; i < n; i++) encoded[i] = provider.EncodeBlockHeader(uncles[i]);
            return RLP.RLP.EncodeList(encoded);
        }

        private static byte[] EncodeWithdrawals(IList<Withdrawal> withdrawals)
        {
            if (withdrawals == null) return Array.Empty<byte>();
            var encoded = new byte[withdrawals.Count][];
            for (int i = 0; i < withdrawals.Count; i++) encoded[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);
            return RLP.RLP.EncodeList(encoded);
        }
    }
}
