using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.DevChain.Storage.Sqlite
{
    /// <summary>
    /// EIP-7928: the block access list is committed to by `header.block_access_list_hash` but is NOT
    /// part of the block body. A peer can re-serve uncles or withdrawals from a body forever; a BAL
    /// exists nowhere but in the store of whoever produced or imported the block. On DevChain the
    /// authoring node holds the only copy in existence.
    /// </summary>
    public class SqliteBlockAccessListStore : IBlockAccessListStore
    {
        private readonly SqliteStorageManager _manager;
        private readonly IBlockStore _blockStore;

        public SqliteBlockAccessListStore(SqliteStorageManager manager, IBlockStore blockStore)
        {
            _manager = manager;
            _blockStore = blockStore;
        }

        public Task SaveAsync(byte[] blockHash, byte[] blockAccessListRlp)
        {
            if (blockHash == null || blockAccessListRlp == null) return Task.CompletedTask;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO block_access_lists (block_hash, bal_data) VALUES (@hash, @data)";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());
            cmd.Parameters.AddWithValue("@data", blockAccessListRlp);
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        public Task<byte[]> GetByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.FromResult<byte[]>(null);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT bal_data FROM block_access_lists WHERE block_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());

            return Task.FromResult(cmd.ExecuteScalar() as byte[]);
        }

        public async Task<byte[]> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockHash = await ResolveCanonicalHashAsync(blockNumber).ConfigureAwait(false);
            if (blockHash == null) return null;
            return await GetByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        public Task DeleteByBlockHashAsync(byte[] blockHash)
        {
            if (blockHash == null) return Task.CompletedTask;

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM block_access_lists WHERE block_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        public async Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            var blockHash = await ResolveCanonicalHashAsync(blockNumber).ConfigureAwait(false);
            if (blockHash != null) await DeleteByBlockHashAsync(blockHash).ConfigureAwait(false);
        }

        private async Task<byte[]> ResolveCanonicalHashAsync(BigInteger blockNumber)
        {
            if (_blockStore == null) return null;
            return await _blockStore.GetHashByNumberAsync(blockNumber).ConfigureAwait(false);
        }
    }
}
