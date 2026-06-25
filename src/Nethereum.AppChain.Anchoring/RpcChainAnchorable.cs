using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Web3;

namespace Nethereum.AppChain.Anchoring
{
    public class RpcChainAnchorable : IChainAnchorable
    {
        private readonly IWeb3 _web3;
        private readonly object _cacheLock = new object();
        private BigInteger _cachedBlockNumber = -1;
        private byte[] _cachedBlockHash;

        public RpcChainAnchorable(IWeb3 web3)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            var result = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().ConfigureAwait(false);
            return result.Value;
        }

        public async Task<BlockHeader> GetBlockByNumberAsync(BigInteger blockNumber)
        {
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber)).ConfigureAwait(false);
            if (block == null) return null;

            lock (_cacheLock)
            {
                _cachedBlockNumber = blockNumber;
                _cachedBlockHash = block.BlockHash?.HexToByteArray();
            }

            return new BlockHeader
            {
                ParentHash = block.ParentHash?.HexToByteArray(),
                StateRoot = block.StateRoot?.HexToByteArray(),
                TransactionsHash = block.TransactionsRoot?.HexToByteArray(),
                ReceiptHash = block.ReceiptsRoot?.HexToByteArray(),
                GasUsed = (long)block.GasUsed.Value,
            };
        }

        public async Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber)
        {
            lock (_cacheLock)
            {
                if (_cachedBlockNumber == blockNumber && _cachedBlockHash != null)
                    return _cachedBlockHash;
            }

            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber)).ConfigureAwait(false);
            if (block == null) return null;

            var hash = block.BlockHash?.HexToByteArray();
            lock (_cacheLock)
            {
                _cachedBlockHash = hash;
                _cachedBlockNumber = blockNumber;
            }
            return hash;
        }
    }
}
