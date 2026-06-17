using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.ModelFactories;

namespace Nethereum.BlockReplay
{
    /// <summary>
    /// Minimal RPC-backed <see cref="IBlockStore"/> for BlockReplay. Provides
    /// the ancestor-header + block-hash lookups the engine needs
    /// (ResolveParentStateRootAsync, pre-EIP-2935 BLOCKHASH). Caches every
    /// fetched header in memory so a multi-block replay session doesn't
    /// re-hit the RPC for the same ancestor twice.
    /// </summary>
    public sealed class RpcBlockStore : IBlockStore
    {
        private readonly IEthApiService _eth;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, BlockHeader> _byHash = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, byte[]> _hashByNumber = new();

        public RpcBlockStore(IEthApiService eth)
        {
            _eth = eth ?? throw new ArgumentNullException(nameof(eth));
        }

        public async Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            if (hash == null) return null;
            var key = hash.ToHex();
            if (_byHash.TryGetValue(key, out var cached)) return cached;
            var hex = "0x" + key;
            var rpcBlock = await _eth.Blocks.GetBlockWithTransactionsHashesByHash.SendRequestAsync(hex).ConfigureAwait(false);
            if (rpcBlock == null) return null;
            var header = BlockHeaderRPCFactory.FromRPC(rpcBlock);
            ApplyExtraFields(header, rpcBlock);
            _byHash[key] = header;
            _hashByNumber[(long)header.BlockNumber] = hash;
            return header;
        }

        public async Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            var rpcBlock = await _eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(number))).ConfigureAwait(false);
            if (rpcBlock == null) return null;
            var header = BlockHeaderRPCFactory.FromRPC(rpcBlock);
            ApplyExtraFields(header, rpcBlock);
            var hash = rpcBlock.BlockHash.HexToByteArray();
            _byHash[hash.ToHex()] = header;
            _hashByNumber[(long)number] = hash;
            return header;
        }

        public async Task<byte[]> GetHashByNumberAsync(BigInteger number)
        {
            if (_hashByNumber.TryGetValue((long)number, out var cached)) return cached;
            var rpcBlock = await _eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new BlockParameter(new HexBigInteger(number))).ConfigureAwait(false);
            if (rpcBlock == null) return null;
            var hash = rpcBlock.BlockHash.HexToByteArray();
            _hashByNumber[(long)number] = hash;
            return hash;
        }

        public Task<BlockHeader> GetLatestAsync() => throw new NotSupportedException("BlockReplay does not need GetLatestAsync.");
        public Task<BigInteger> GetHeightAsync() => throw new NotSupportedException("BlockReplay does not need GetHeightAsync.");
        public Task SaveAsync(BlockHeader header, byte[] blockHash) => Task.CompletedTask; // no-op; RPC is the source of truth
        public Task<bool> ExistsAsync(byte[] hash) => Task.FromResult(_byHash.ContainsKey(hash.ToHex()));
        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => throw new NotSupportedException();
        public Task DeleteByNumberAsync(BigInteger blockNumber) => throw new NotSupportedException();

        private static void ApplyExtraFields(BlockHeader header, Block rpcBlock)
        {
            // Fields BlockHeaderRPCFactory doesn't set (post-Shanghai / Cancun / Prague).
            if (rpcBlock.WithdrawalsRoot != null && !string.IsNullOrEmpty(rpcBlock.WithdrawalsRoot.HexValue))
                header.WithdrawalsRoot = rpcBlock.WithdrawalsRoot.HexValue.HexToByteArray();
            if (!string.IsNullOrEmpty(rpcBlock.ParentBeaconBlockRoot))
                header.ParentBeaconBlockRoot = rpcBlock.ParentBeaconBlockRoot.HexToByteArray();
            if (rpcBlock.BlobGasUsed != null)
                header.BlobGasUsed = (long)rpcBlock.BlobGasUsed.Value;
            if (rpcBlock.ExcessBlobGas != null)
                header.ExcessBlobGas = (long)rpcBlock.ExcessBlobGas.Value;
            if (!string.IsNullOrEmpty(rpcBlock.RequestsHash))
                header.RequestsHash = rpcBlock.RequestsHash.HexToByteArray();
        }
    }
}
