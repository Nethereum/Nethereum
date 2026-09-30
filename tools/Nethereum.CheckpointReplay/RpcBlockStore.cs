using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.DebugNode;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CheckpointReplay
{
    public sealed class RpcBlockStore : IBlockStore
    {
        private readonly IEthApiService _eth;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, BlockHeader> _byHash = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, byte[]> _hashByNumber = new();

        private readonly IDebugGetRawHeader _rawHeader;

        public RpcBlockStore(IEthApiService eth)
        {
            _eth = eth ?? throw new ArgumentNullException(nameof(eth));
            _rawHeader = new DebugGetRawHeader(eth.Client);
        }

        public async Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            if (hash == null) return null;
            var key = hash.ToHex();
            if (_byHash.TryGetValue(key, out var cached)) return cached;
            var header = (await FetchHeaderAsync(new BlockParameter(new HexBigInteger("0x" + key))).ConfigureAwait(false)).Header;
            if (header == null) return null;
            _byHash[key] = header;
            _hashByNumber[(long)header.BlockNumber] = hash;
            return header;
        }

        public async Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            var fetched = await FetchHeaderAsync(new BlockParameter(new HexBigInteger(number))).ConfigureAwait(false);
            if (fetched.Header == null) return null;
            var header = fetched.Header;
            var hash = fetched.HashOfTheBytesTheNodeHashed;
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

        public Task<BlockHeader> GetLatestAsync() => throw new NotSupportedException();
        public Task<BigInteger> GetHeightAsync() => throw new NotSupportedException();
        public Task SaveAsync(BlockHeader header, byte[] blockHash) => Task.CompletedTask;
        private readonly struct FetchedHeader
        {
            public FetchedHeader(BlockHeader header, byte[] hashOfTheBytesTheNodeHashed)
            {
                Header = header;
                HashOfTheBytesTheNodeHashed = hashOfTheBytesTheNodeHashed;
            }

            public BlockHeader Header { get; }

            public byte[] HashOfTheBytesTheNodeHashed { get; }
        }

        private async Task<FetchedHeader> FetchHeaderAsync(BlockParameter block)
        {
            var rawHeader = await _rawHeader.SendRequestAsync(block).ConfigureAwait(false);
            if (string.IsNullOrEmpty(rawHeader) || rawHeader == "0x") return new FetchedHeader(null, null);

            var rawBytes = rawHeader.HexToByteArray();
            var header = BlockHeaderEncoder.Current.Decode(rawBytes);
            var hash = new Nethereum.Util.Sha3Keccack().CalculateHash(rawBytes);

            RefuseAHeaderThatDoesNotReEncodeToItself(header, hash);

            return new FetchedHeader(header, hash);
        }

        private static void RefuseAHeaderThatDoesNotReEncodeToItself(BlockHeader header, byte[] hashOfTheNodesBytes)
        {
            var reEncoded = BlockHashCalculator.ForHeader(header);
            if (reEncoded.ToHex() == hashOfTheNodesBytes.ToHex()) return;

            throw new InvalidOperationException(
                "The header decoded from the node's own bytes re-encodes to a different hash: node " +
                hashOfTheNodesBytes.ToHex(true) + ", ours " + reEncoded.ToHex(true) +
                ". The header codec disagrees with the node, and every replay from here would be " +
                "measured against our own encoding rather than the chain's.");
        }

        public Task<bool> ExistsAsync(byte[] hash) => Task.FromResult(_byHash.ContainsKey(hash.ToHex()));
        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => throw new NotSupportedException();
        public Task DeleteByNumberAsync(BigInteger blockNumber) => throw new NotSupportedException();

    }
}
