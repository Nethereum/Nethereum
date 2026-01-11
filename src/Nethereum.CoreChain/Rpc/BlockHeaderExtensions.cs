using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public static class BlockHeaderExtensions
    {
        public static BlockWithTransactionHashes ToBlockWithTransactionHashes(this BlockHeader header, byte[] blockHash, string[] transactionHashes = null)
        {
            return new BlockWithTransactionHashes
            {
                Number = new HexBigInteger(header.BlockNumber),
                BlockHash = blockHash?.ToHex(true),
                ParentHash = header.ParentHash?.ToHex(true),
                Nonce = "0x0000000000000000",
                Sha3Uncles = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
                LogsBloom = header.LogsBloom?.ToHex(true),
                TransactionsRoot = header.TransactionsHash?.ToHex(true),
                StateRoot = header.StateRoot?.ToHex(true),
                ReceiptsRoot = header.ReceiptHash?.ToHex(true),
                Miner = header.Coinbase,
                Difficulty = new HexBigInteger(header.Difficulty),
                TotalDifficulty = new HexBigInteger(0),
                MixHash = header.MixHash?.ToHex(true),
                ExtraData = header.ExtraData?.ToHex(true) ?? "0x",
                Size = new HexBigInteger(0),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                Uncles = new string[0],
                BaseFeePerGas = header.BaseFee.HasValue ? new HexBigInteger(header.BaseFee.Value) : null,
                TransactionHashes = transactionHashes ?? new string[0]
            };
        }

        public static BlockWithTransactions ToBlockWithTransactions(this BlockHeader header, byte[] blockHash, Transaction[] transactions = null)
        {
            return new BlockWithTransactions
            {
                Number = new HexBigInteger(header.BlockNumber),
                BlockHash = blockHash?.ToHex(true),
                ParentHash = header.ParentHash?.ToHex(true),
                Nonce = "0x0000000000000000",
                Sha3Uncles = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347",
                LogsBloom = header.LogsBloom?.ToHex(true),
                TransactionsRoot = header.TransactionsHash?.ToHex(true),
                StateRoot = header.StateRoot?.ToHex(true),
                ReceiptsRoot = header.ReceiptHash?.ToHex(true),
                Miner = header.Coinbase,
                Difficulty = new HexBigInteger(header.Difficulty),
                TotalDifficulty = new HexBigInteger(0),
                MixHash = header.MixHash?.ToHex(true),
                ExtraData = header.ExtraData?.ToHex(true) ?? "0x",
                Size = new HexBigInteger(0),
                GasLimit = new HexBigInteger(header.GasLimit),
                GasUsed = new HexBigInteger(header.GasUsed),
                Timestamp = new HexBigInteger(header.Timestamp),
                Uncles = new string[0],
                BaseFeePerGas = header.BaseFee.HasValue ? new HexBigInteger(header.BaseFee.Value) : null,
                Transactions = transactions ?? new Transaction[0]
            };
        }
    }
}
