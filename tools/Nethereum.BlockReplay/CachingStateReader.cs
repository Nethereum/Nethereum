using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.BlockReplay
{
    /// <summary>
    /// IStateReader decorator that consults a local <see cref="RpcReplayCache"/>
    /// before delegating to the inner reader. On miss, fetches from the inner
    /// reader and stores the result so future replays of the same parent
    /// block hit local disk instead of the RPC node.
    /// </summary>
    public sealed class CachingStateReader : IStateReader, IAccountStorageReader
    {
        private readonly IStateReader _inner;
        private readonly RpcReplayCache _cache;
        private readonly long _parentBlock;

        public CachingStateReader(IStateReader inner, RpcReplayCache cache, long parentBlock)
        {
            _inner = inner;
            _cache = cache;
            _parentBlock = parentBlock;
        }

        /// <summary>
        /// Forwarded so this decorator does not STRIP the capability from a reader that has it.
        /// The `is IAccountStorageReader` probe in ExecutionStateService cannot see through a
        /// decorator, so a wrapper declaring only IStateReader silently disables the EIP-7610
        /// collision check - which is how this instrument came to deploy where the node collides,
        /// and so could not observe the very divergence it exists to catch.
        ///
        /// <para>Deliberately NOT cached: the answer is a property of the whole storage trie rather
        /// than of a named slot, so the per-slot cache has nothing to key it on, and a wrong cached
        /// answer here allows a creation that must fail. An inner reader that cannot answer returns
        /// false, exactly as before.</para>
        /// </summary>
        public Task<bool> AccountHasStorageAsync(string address) =>
            _inner is IAccountStorageReader storageReader
                ? storageReader.AccountHasStorageAsync(address)
                : Task.FromResult(false);

        public async Task<EvmUInt256> GetBalanceAsync(string address)
        {
            if (_cache.TryGetBalance(_parentBlock, address, out var cached))
                return EvmUInt256.FromBigEndian(cached);
            var val = await _inner.GetBalanceAsync(address);
            _cache.PutBalance(_parentBlock, address, val.ToBigEndian());
            return val;
        }

        public Task<EvmUInt256> GetBalanceAsync(byte[] address)
            => GetBalanceAsync(address.ConvertToEthereumChecksumAddress());

        public async Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            if (_cache.TryGetNonce(_parentBlock, address, out var cached))
                return EvmUInt256.FromBigEndian(cached);
            var val = await _inner.GetTransactionCountAsync(address);
            _cache.PutNonce(_parentBlock, address, val.ToBigEndian());
            return val;
        }

        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address)
            => GetTransactionCountAsync(address.ConvertToEthereumChecksumAddress());

        public async Task<byte[]> GetCodeAsync(string address)
        {
            if (_cache.TryGetCode(_parentBlock, address, out var cached))
                return cached;
            var val = await _inner.GetCodeAsync(address) ?? new byte[0];
            _cache.PutCode(_parentBlock, address, val);
            return val;
        }

        public Task<byte[]> GetCodeAsync(byte[] address)
            => GetCodeAsync(address.ConvertToEthereumChecksumAddress());

        public async Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
        {
            var slotBE = position.ToBigEndian();
            var slotKey = slotBE.Length == 32 ? slotBE : PadLeftTo32(slotBE);
            if (_cache.TryGetStorage(_parentBlock, address, slotKey, out var cached))
                return cached;
            var val = await _inner.GetStorageAtAsync(address, position) ?? new byte[0];
            _cache.PutStorage(_parentBlock, address, slotKey, val);
            return val;
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position)
            => GetStorageAtAsync(address.ConvertToEthereumChecksumAddress(), position);

        public async Task<byte[]> GetBlockHashAsync(long blockNumber)
        {
            if (_cache.TryGetBlockHash(blockNumber, out var cached))
                return cached;
            var val = await _inner.GetBlockHashAsync(blockNumber);
            if (val != null) _cache.PutBlockHash(blockNumber, val);
            return val;
        }

        public async Task<bool> AccountExistsAsync(string address)
        {
            // EIP-161 existence check: any of balance/nonce/code non-empty.
            // Use cached lookups so subsequent calls don't hit RPC.
            var bal = await GetBalanceAsync(address);
            if (!bal.IsZero) return true;
            var nonce = await GetTransactionCountAsync(address);
            if (!nonce.IsZero) return true;
            var code = await GetCodeAsync(address);
            return code != null && code.Length > 0;
        }

        private static byte[] PadLeftTo32(byte[] src)
        {
            if (src.Length >= 32) return src;
            var dst = new byte[32];
            System.Buffer.BlockCopy(src, 0, dst, 32 - src.Length, src.Length);
            return dst;
        }
    }
}
