using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public static class BlockHeaderExtensions
    {
        public static BlockWithTransactionHashes ToBlockWithTransactionHashes(
            this BlockHeader header,
            byte[] blockHash,
            string[] transactionHashes = null,
            BigInteger? totalDifficulty = null,
            int? blockSize = null)
        {
            var calculatedTotalDifficulty = totalDifficulty ?? header.Difficulty * (header.BlockNumber + 1);
            var calculatedSize = blockSize ?? CalculateHeaderSize(header);

            return new BlockWithTransactionHashes
            {
                Number = new HexBigInteger(header.BlockNumber),
                BlockHash = blockHash?.ToHex(true),
                ParentHash = header.ParentHash?.ToHex(true),
                Nonce = FormatNonce(header.Nonce),
                Sha3Uncles = header.UnclesHash?.ToHex(true) ?? EmptyUnclesHashHex,
                LogsBloom = header.LogsBloom?.ToHex(true),
                TransactionsRoot = header.TransactionsHash?.ToHex(true),
                StateRoot = header.StateRoot?.ToHex(true),
                ReceiptsRoot = header.ReceiptHash?.ToHex(true),
                Miner = FormatAddress(header.Coinbase),
                Difficulty = new HexBigInteger(header.Difficulty),
                TotalDifficulty = new HexBigInteger(calculatedTotalDifficulty),
                MixHash = header.MixHash?.ToHex(true),
                ExtraData = header.ExtraData?.ToHex(true) ?? "0x",
                Size = new HexBigInteger(calculatedSize),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                Uncles = new string[0],
                BaseFeePerGas = header.BaseFee.HasValue ? new HexBigInteger(header.BaseFee.Value) : null,
                TransactionHashes = transactionHashes ?? new string[0]
            };
        }

        public static BlockWithTransactions ToBlockWithTransactions(
            this BlockHeader header,
            byte[] blockHash,
            Transaction[] transactions = null,
            BigInteger? totalDifficulty = null,
            int? blockSize = null)
        {
            var calculatedTotalDifficulty = totalDifficulty ?? header.Difficulty * (header.BlockNumber + 1);
            var calculatedSize = blockSize ?? CalculateHeaderSize(header);

            return new BlockWithTransactions
            {
                Number = new HexBigInteger(header.BlockNumber),
                BlockHash = blockHash?.ToHex(true),
                ParentHash = header.ParentHash?.ToHex(true),
                Nonce = FormatNonce(header.Nonce),
                Sha3Uncles = header.UnclesHash?.ToHex(true) ?? EmptyUnclesHashHex,
                LogsBloom = header.LogsBloom?.ToHex(true),
                TransactionsRoot = header.TransactionsHash?.ToHex(true),
                StateRoot = header.StateRoot?.ToHex(true),
                ReceiptsRoot = header.ReceiptHash?.ToHex(true),
                Miner = FormatAddress(header.Coinbase),
                Difficulty = new HexBigInteger(header.Difficulty),
                TotalDifficulty = new HexBigInteger(calculatedTotalDifficulty),
                MixHash = header.MixHash?.ToHex(true),
                ExtraData = header.ExtraData?.ToHex(true) ?? "0x",
                Size = new HexBigInteger(calculatedSize),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                Uncles = new string[0],
                BaseFeePerGas = header.BaseFee.HasValue ? new HexBigInteger(header.BaseFee.Value) : null,
                Transactions = transactions ?? new Transaction[0]
            };
        }

        // keccak256(rlp([])) — the canonical empty-uncle hash used in headers that have no uncles.
        private const string EmptyUnclesHashHex =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347";

        // PoW nonce is always 8 bytes per JSON-RPC spec (left-padded with zeros).
        // Pre-merge headers carry the PoW solution; post-merge headers carry zeros.
        private static string FormatNonce(byte[] nonce)
        {
            if (nonce == null || nonce.Length == 0)
                return "0x0000000000000000";
            if (nonce.Length == 8)
                return nonce.ToHex(true);
            // Left-pad to 8 bytes if a shorter encoding slipped through.
            var padded = new byte[8];
            System.Array.Copy(nonce, 0, padded, 8 - nonce.Length, nonce.Length);
            return padded.ToHex(true);
        }

        private static string FormatAddress(string coinbase)
        {
            if (string.IsNullOrEmpty(coinbase)) return coinbase;
            return coinbase.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)
                ? coinbase
                : "0x" + coinbase;
        }

        // Header-only fallback. Used when callers can't supply body parts (subscriptions,
        // hash-only modes that don't load bodies). Block size in canonical eth_getBlockByNumber
        // is the full RLP block size — see CalculateFullBlockSize — but this is the best
        // estimate when only the header is on hand.
        private static int CalculateHeaderSize(BlockHeader header)
        {
            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(header);
            return encoded.Length;
        }

        /// <summary>
        /// Compute the canonical eth_getBlockByNumber "size" field: the length of the
        /// RLP-encoded block (<c>[header, transactions, uncles, withdrawals?]</c>). Pass the
        /// signed transactions and uncle headers that were stored for this block; pass
        /// null/empty when none. Withdrawals only encoded for post-Shanghai blocks where
        /// <paramref name="includeWithdrawals"/> is true (caller decides based on fork).
        /// </summary>
        public static int CalculateFullBlockSize(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<Nethereum.Model.Withdrawal> withdrawals = null,
            bool includeWithdrawals = false)
        {
            var encodedHeader = BlockHeaderEncoder.Current.Encode(header);

            var txCount = transactions?.Count ?? 0;
            var encodedTxs = new byte[txCount][];
            for (int i = 0; i < txCount; i++)
            {
                // Body encoding matches the eth/68 BlockBodies wire format:
                // typed transactions (EIP-2718) are wrapped as RLP byte strings;
                // legacy transactions stay as nested RLP lists.
                var raw = transactions[i].GetRLPEncoded();
                encodedTxs[i] = (raw.Length > 0 && raw[0] < 0xc0)
                    ? RLP.RLP.EncodeElement(raw)
                    : raw;
            }

            var uncleCount = uncles?.Count ?? 0;
            var encodedUncles = new byte[uncleCount][];
            for (int i = 0; i < uncleCount; i++)
                encodedUncles[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);

            byte[] encodedBlock;
            if (includeWithdrawals && withdrawals != null)
            {
                var encodedWithdrawals = new byte[withdrawals.Count][];
                for (int i = 0; i < withdrawals.Count; i++)
                    encodedWithdrawals[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);

                encodedBlock = RLP.RLP.EncodeList(
                    encodedHeader,
                    RLP.RLP.EncodeList(encodedTxs),
                    RLP.RLP.EncodeList(encodedUncles),
                    RLP.RLP.EncodeList(encodedWithdrawals));
            }
            else
            {
                encodedBlock = RLP.RLP.EncodeList(
                    encodedHeader,
                    RLP.RLP.EncodeList(encodedTxs),
                    RLP.RLP.EncodeList(encodedUncles));
            }

            return encodedBlock.Length;
        }
    }
}
