using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Web3;

namespace Nethereum.AppChain.Anchoring
{
    public class RpcChainAnchorable : IChainAnchorable
    {
        private readonly IWeb3 _web3;

        public RpcChainAnchorable(IWeb3 web3)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            var result = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            return result.Value;
        }

        public async Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber)
        {
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber));
            if (block == null) return null;

            return new BlockHeader
            {
                ParentHash = Nethereum.Hex.HexConvertors.Extensions
                    .HexByteConvertorExtensions.HexToByteArray(block.ParentHash),
                StateRoot = Nethereum.Hex.HexConvertors.Extensions
                    .HexByteConvertorExtensions.HexToByteArray(block.StateRoot),
                TransactionsHash = Nethereum.Hex.HexConvertors.Extensions
                    .HexByteConvertorExtensions.HexToByteArray(block.TransactionsRoot),
                ReceiptHash = Nethereum.Hex.HexConvertors.Extensions
                    .HexByteConvertorExtensions.HexToByteArray(block.ReceiptsRoot),
                GasUsed = (long)block.GasUsed.Value,
            };
        }

        public async Task<byte[]?> GetBlockHashByNumberAsync(BigInteger blockNumber)
        {
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber));
            if (block == null) return null;

            return Nethereum.Hex.HexConvertors.Extensions
                .HexByteConvertorExtensions.HexToByteArray(block.BlockHash);
        }
    }
}
