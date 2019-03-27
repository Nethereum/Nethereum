using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.ModelFactories
{
    public class BlockHeaderRPCFactory
    {
        public static BlockHeader FromRPC(Block rpcBlock)
        {
            var blockHeader = new BlockHeader();
            blockHeader.BlockNumber = rpcBlock.Number;
            blockHeader.Coinbase = rpcBlock.Miner;
            blockHeader.Difficulty = rpcBlock.Difficulty;
            blockHeader.ExtraData = rpcBlock.ExtraData.HexToByteArray();
            blockHeader.GasLimit = (long)rpcBlock.GasLimit.Value;
            blockHeader.GasUsed = (long)rpcBlock.GasUsed.Value;
            blockHeader.LogsBloom = rpcBlock.LogsBloom.HexToByteArray();
            blockHeader.MixHash = rpcBlock.MixHash.HexToByteArray();
            blockHeader.Nonce = rpcBlock.Nonce.HexToByteArray();
            blockHeader.ParentHash = rpcBlock.ParentHash.HexToByteArray();
            blockHeader.ReceiptHash = rpcBlock.ReceiptsRoot.HexToByteArray();
            blockHeader.StateRoot = rpcBlock.StateRoot.HexToByteArray();
            blockHeader.Timestamp = (long)rpcBlock.Timestamp.Value;
            blockHeader.TransactionsHash = rpcBlock.TransactionsRoot.HexToByteArray();
            blockHeader.UnclesHash = rpcBlock.Sha3Uncles.HexToByteArray();
            return blockHeader;
        }
    }
}