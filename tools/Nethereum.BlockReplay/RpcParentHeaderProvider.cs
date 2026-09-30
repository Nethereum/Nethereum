using System;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockReplay
{
    /// <summary>
    /// Minimal <see cref="ITrustedHeaderProvider"/> for single-block replay:
    /// returns the parent block's header (block N-1) as the trusted root for
    /// proof verification during the replay of block N. The block hash + state
    /// root come straight from the RPC node we're already trusting as the
    /// canonical source.
    /// </summary>
    internal sealed class RpcParentHeaderProvider : ITrustedHeaderProvider
    {
        private readonly TrustedExecutionHeader _header;

        private RpcParentHeaderProvider(TrustedExecutionHeader header) { _header = header; }

        public static async Task<RpcParentHeaderProvider> CreateAsync(IEthApiService eth, long parentBlockNumber)
        {
            var rpcBlock = await eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(parentBlockNumber))).ConfigureAwait(false);
            if (rpcBlock == null)
                throw new InvalidOperationException($"RPC did not return parent block {parentBlockNumber}");

            var header = new TrustedExecutionHeader
            {
                BlockHash = rpcBlock.BlockHash.HexToByteArray(),
                BlockNumber = (ulong)parentBlockNumber,
                StateRoot = rpcBlock.StateRoot.HexToByteArray(),
                ReceiptsRoot = rpcBlock.ReceiptsRoot.HexToByteArray(),
                Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)rpcBlock.Timestamp.Value)
            };
            return new RpcParentHeaderProvider(header);
        }

        public TrustedExecutionHeader GetLatestFinalized() => _header;
        public TrustedExecutionHeader GetLatestOptimistic() => _header;
        public byte[] GetBlockHash(ulong blockNumber)
            => blockNumber == _header.BlockNumber ? _header.BlockHash : null;
    }
}
