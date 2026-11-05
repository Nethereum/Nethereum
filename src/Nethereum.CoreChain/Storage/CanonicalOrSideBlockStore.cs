using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public sealed class CanonicalOrSideBlockStore : IBlockStore
    {
        private readonly IBlockStore _canonical;
        private readonly ISideBlockStore _side;

        public CanonicalOrSideBlockStore(IBlockStore canonical, ISideBlockStore side)
        {
            _canonical = canonical ?? throw new ArgumentNullException(nameof(canonical));
            _side = side ?? throw new ArgumentNullException(nameof(side));
        }

        public async Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            var header = await _canonical.GetByHashAsync(hash).ConfigureAwait(false);
            if (header != null) return header;
            return _side.TryGet(hash, out var block) ? block.Header : null!;
        }

        public async Task<bool> ExistsAsync(byte[] hash)
            => await _canonical.ExistsAsync(hash).ConfigureAwait(false) || _side.Contains(hash);

        public Task<BlockHeader> GetByNumberAsync(BigInteger number) => _canonical.GetByNumberAsync(number);
        public Task<BlockHeader> GetLatestAsync() => _canonical.GetLatestAsync();
        public Task<BigInteger> GetHeightAsync() => _canonical.GetHeightAsync();
        public Task SaveAsync(BlockHeader header, byte[] blockHash) => _canonical.SaveAsync(header, blockHash);
        public Task<byte[]> GetHashByNumberAsync(BigInteger number) => _canonical.GetHashByNumberAsync(number);
        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => _canonical.UpdateBlockHashAsync(blockNumber, newHash);
        public Task DeleteByNumberAsync(BigInteger blockNumber) => _canonical.DeleteByNumberAsync(blockNumber);
    }
}
