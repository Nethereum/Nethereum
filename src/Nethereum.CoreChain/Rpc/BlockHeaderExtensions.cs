using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RPC.Eth.DTOs;
using ModelWithdrawal = Nethereum.Model.Withdrawal;
using RpcWithdrawal = Nethereum.RPC.Eth.DTOs.Withdrawal;

namespace Nethereum.CoreChain.Rpc
{
    public static class BlockHeaderExtensions
    {
        public static BlockWithTransactionHashes ToBlockWithTransactionHashes(
            this BlockHeader header,
            byte[] blockHash,
            string[] transactionHashes,
            int blockSize,
            IList<ModelWithdrawal> withdrawals = null)
        {
            var block = new BlockWithTransactionHashes
            {
                TransactionHashes = transactionHashes ?? new string[0]
            };
            PopulateCommonFields(block, header, blockHash, blockSize, withdrawals, uncles: null);
            return block;
        }

        public static BlockWithTransactionHashes ToBlockWithTransactionHashes(
            this BlockHeader header,
            byte[] blockHash,
            string[] transactionHashes,
            int blockSize,
            IList<ModelWithdrawal> withdrawals,
            IList<BlockHeader> uncles)
        {
            var block = new BlockWithTransactionHashes
            {
                TransactionHashes = transactionHashes ?? new string[0]
            };
            PopulateCommonFields(block, header, blockHash, blockSize, withdrawals, uncles);
            return block;
        }

        public static BlockWithTransactions ToBlockWithTransactions(
            this BlockHeader header,
            byte[] blockHash,
            Transaction[] transactions,
            int blockSize,
            IList<ModelWithdrawal> withdrawals = null)
        {
            var block = new BlockWithTransactions
            {
                Transactions = transactions ?? new Transaction[0]
            };
            PopulateCommonFields(block, header, blockHash, blockSize, withdrawals, uncles: null);
            return block;
        }

        [Obsolete("Use the overload taking a real int blockSize and withdrawals (and, for uncles, the 6-arg overload).")]
        public static BlockWithTransactionHashes ToBlockWithTransactionHashes(
            this BlockHeader header,
            byte[] blockHash,
            string[] transactionHashes = null,
            BigInteger? totalDifficulty = null,
            int? blockSize = null)
        {
            var block = new BlockWithTransactionHashes
            {
                TransactionHashes = transactionHashes ?? new string[0]
            };
            PopulateCommonFields(block, header, blockHash, blockSize ?? CalculateHeaderSize(header), withdrawals: null, uncles: null);
            if (totalDifficulty.HasValue) block.TotalDifficulty = new HexBigInteger(totalDifficulty.Value);
            return block;
        }

        [Obsolete("Use the overload taking a real int blockSize and withdrawals (and, for uncles, the 6-arg overload).")]
        public static BlockWithTransactions ToBlockWithTransactions(
            this BlockHeader header,
            byte[] blockHash,
            Transaction[] transactions = null,
            BigInteger? totalDifficulty = null,
            int? blockSize = null)
        {
            var block = new BlockWithTransactions
            {
                Transactions = transactions ?? new Transaction[0]
            };
            PopulateCommonFields(block, header, blockHash, blockSize ?? CalculateHeaderSize(header), withdrawals: null, uncles: null);
            if (totalDifficulty.HasValue) block.TotalDifficulty = new HexBigInteger(totalDifficulty.Value);
            return block;
        }

        public static BlockWithTransactions ToBlockWithTransactions(
            this BlockHeader header,
            byte[] blockHash,
            Transaction[] transactions,
            int blockSize,
            IList<ModelWithdrawal> withdrawals,
            IList<BlockHeader> uncles)
        {
            var block = new BlockWithTransactions
            {
                Transactions = transactions ?? new Transaction[0]
            };
            PopulateCommonFields(block, header, blockHash, blockSize, withdrawals, uncles);
            return block;
        }

        internal static void PopulateCommonFields(
            Block block, BlockHeader header, byte[] blockHash, int blockSize, IList<ModelWithdrawal> withdrawals,
            IList<BlockHeader> uncles)
        {
            block.Number = new HexBigInteger(header.BlockNumber);
            block.BlockHash = blockHash?.ToHex(true);
            block.ParentHash = header.ParentHash?.ToHex(true);
            block.Nonce = FormatNonce(header.Nonce);
            block.Sha3Uncles = header.UnclesHash?.ToHex(true) ?? EmptyUnclesHashHex;
            block.LogsBloom = header.LogsBloom?.ToHex(true);
            block.TransactionsRoot = header.TransactionsHash?.ToHex(true);
            block.StateRoot = header.StateRoot?.ToHex(true);
            block.ReceiptsRoot = header.ReceiptHash?.ToHex(true);
            block.Miner = RpcTransactionAddress.Normalize(header.Coinbase);
            block.Difficulty = new HexBigInteger(header.Difficulty);
            block.MixHash = header.MixHash?.ToHex(true);
            block.ExtraData = header.ExtraData?.ToHex(true) ?? "0x";
            block.Size = new HexBigInteger(blockSize);
            block.GasLimit = new HexBigInteger(header.GasLimit);
            block.GasUsed = new HexBigInteger(header.GasUsed);
            block.Timestamp = new HexBigInteger(header.Timestamp);
            block.Uncles = BuildUncleHashes(uncles);
            block.BaseFeePerGas = header.BaseFee.HasValue ? new HexBigInteger(header.BaseFee.Value) : null;
            block.WithdrawalsRoot = header.WithdrawalsRoot != null ? new HexBigInteger(header.WithdrawalsRoot.ToHex(true)) : null;
            block.Withdrawals = header.WithdrawalsRoot != null ? BuildRpcWithdrawals(withdrawals) : null;
            block.ParentBeaconBlockRoot = header.ParentBeaconBlockRoot?.ToHex(true);
            block.BlobGasUsed = header.BlobGasUsed.HasValue ? new HexBigInteger(header.BlobGasUsed.Value) : null;
            block.ExcessBlobGas = header.ExcessBlobGas.HasValue ? new HexBigInteger(header.ExcessBlobGas.Value) : null;
            block.RequestsHash = header.RequestsHash?.ToHex(true);
            block.BlockAccessListHash = header.BlockAccessListHash?.ToHex(true);
            block.SlotNumber = header.SlotNumber.HasValue ? new HexBigInteger(header.SlotNumber.Value) : null;
        }

        private static string[] BuildUncleHashes(IList<BlockHeader> uncles)
        {
            if (uncles == null || uncles.Count == 0) return new string[0];

            var result = new string[uncles.Count];
            for (var i = 0; i < uncles.Count; i++)
            {
                result[i] = BlockHashCalculator.ForHeader(uncles[i]).ToHex(true);
            }
            return result;
        }

        private static RpcWithdrawal[] BuildRpcWithdrawals(IList<ModelWithdrawal> withdrawals)
        {
            if (withdrawals == null) return new RpcWithdrawal[0];

            var result = new RpcWithdrawal[withdrawals.Count];
            for (var i = 0; i < withdrawals.Count; i++)
            {
                var withdrawal = withdrawals[i];
                result[i] = new RpcWithdrawal
                {
                    Index = new HexBigInteger(withdrawal.Index),
                    ValidatorIndex = new HexBigInteger(withdrawal.ValidatorIndex),
                    Address = withdrawal.Address?.ToHex(true),
                    Amount = new HexBigInteger(withdrawal.AmountInGwei)
                };
            }
            return result;
        }

        private const string EmptyUnclesHashHex =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347";

        private static string FormatNonce(byte[] nonce)
        {
            if (nonce == null || nonce.Length == 0)
                return "0x0000000000000000";
            if (nonce.Length == 8)
                return nonce.ToHex(true);
            var padded = new byte[8];
            System.Array.Copy(nonce, 0, padded, 8 - nonce.Length, nonce.Length);
            return padded.ToHex(true);
        }

        private static int CalculateHeaderSize(BlockHeader header)
        {
            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(header);
            return encoded.Length;
        }

        public static int CalculateFullBlockSize(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<ModelWithdrawal> withdrawals = null)
        {
            var withdrawalsForEncoding = header.WithdrawalsRoot != null
                ? (withdrawals ?? new List<ModelWithdrawal>())
                : null;
            return NewBlockMessageEncoder.EncodeBlock(header, transactions, uncles, withdrawalsForEncoding).Length;
        }
    }
}
