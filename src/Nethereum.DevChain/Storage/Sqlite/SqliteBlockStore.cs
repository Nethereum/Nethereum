using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteBlockStore : IBlockStore
    {
        private readonly SqliteStorageManager _manager;
        private long _savepointCounter;

        public SqliteBlockStore(SqliteStorageManager manager)
        {
            _manager = manager;
        }

        public Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            if (hash == null) return Task.FromResult<BlockHeader>(null);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT header_data FROM blocks WHERE block_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", hash.ToHex());

            var data = cmd.ExecuteScalar() as byte[];
            if (data == null) return Task.FromResult<BlockHeader>(null);

            return Task.FromResult(BlockHeaderEncoder.Current.Decode(data));
        }

        public Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT b.header_data FROM blocks b INNER JOIN block_numbers bn ON b.block_hash = bn.block_hash WHERE bn.block_number = @num";
            cmd.Parameters.AddWithValue("@num", (long)number);

            var data = cmd.ExecuteScalar() as byte[];
            if (data == null) return Task.FromResult<BlockHeader>(null);

            return Task.FromResult(BlockHeaderEncoder.Current.Decode(data));
        }

        public Task<BlockHeader> GetLatestAsync()
        {
            var height = GetHeightInternal();
            if (height < 0) return Task.FromResult<BlockHeader>(null);
            return GetByNumberAsync(height);
        }

        public Task<BigInteger> GetHeightAsync() => Task.FromResult((BigInteger)GetHeightInternal());

        private long GetHeightInternal()
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM metadata WHERE key = 'height'";
            var result = cmd.ExecuteScalar();
            if (result == null || result is System.DBNull) return -1;
            var bytes = (byte[])result;
            return (long)new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }

        public Task SaveAsync(BlockHeader header, byte[] blockHash)
        {
            if (header == null || blockHash == null) return Task.CompletedTask;

            var hashHex = blockHash.ToHex();
            var headerData = BlockHeaderEncoder.Current.Encode(header);
            var blockNum = (long)header.BlockNumber;

            var sp = NextSavepoint();
            ExecuteSql($"SAVEPOINT {sp}");

            try
            {
                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO blocks (block_hash, header_data) VALUES (@hash, @data)";
                    cmd.Parameters.AddWithValue("@hash", hashHex);
                    cmd.Parameters.AddWithValue("@data", headerData);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO block_numbers (block_number, block_hash) VALUES (@num, @hash)";
                    cmd.Parameters.AddWithValue("@num", blockNum);
                    cmd.Parameters.AddWithValue("@hash", hashHex);
                    cmd.ExecuteNonQuery();
                }

                var currentHeight = GetHeightInternal();
                if (blockNum > currentHeight)
                {
                    var heightBytes = header.BlockNumber.ToBigEndian();
                    using var cmd = _manager.Connection.CreateCommand();
                    cmd.CommandText = "INSERT OR REPLACE INTO metadata (key, value) VALUES ('height', @val)";
                    cmd.Parameters.AddWithValue("@val", heightBytes);
                    cmd.ExecuteNonQuery();
                }

                ExecuteSql($"RELEASE SAVEPOINT {sp}");
            }
            catch
            {
                ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                ExecuteSql($"RELEASE SAVEPOINT {sp}");
                throw;
            }

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(byte[] hash)
        {
            if (hash == null) return Task.FromResult(false);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM blocks WHERE block_hash = @hash LIMIT 1";
            cmd.Parameters.AddWithValue("@hash", hash.ToHex());

            return Task.FromResult(cmd.ExecuteScalar() != null);
        }

        public Task<byte[]> GetHashByNumberAsync(BigInteger number)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT block_hash FROM block_numbers WHERE block_number = @num";
            cmd.Parameters.AddWithValue("@num", (long)number);

            var hashHex = cmd.ExecuteScalar() as string;
            if (hashHex == null) return Task.FromResult<byte[]>(null);

            return Task.FromResult(hashHex.HexToByteArray());
        }

        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash)
        {
            if (newHash == null) return Task.CompletedTask;

            var newHashHex = newHash.ToHex();
            var blockNum = (long)blockNumber;

            var sp = NextSavepoint();
            ExecuteSql($"SAVEPOINT {sp}");

            try
            {
                string oldHashHex;
                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT block_hash FROM block_numbers WHERE block_number = @num";
                    cmd.Parameters.AddWithValue("@num", blockNum);
                    oldHashHex = cmd.ExecuteScalar() as string;
                }

                if (oldHashHex == null)
                {
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                    return Task.CompletedTask;
                }

                byte[] headerData;
                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT header_data FROM blocks WHERE block_hash = @hash";
                    cmd.Parameters.AddWithValue("@hash", oldHashHex);
                    headerData = cmd.ExecuteScalar() as byte[];
                }

                if (headerData == null)
                {
                    ExecuteSql($"RELEASE SAVEPOINT {sp}");
                    return Task.CompletedTask;
                }

                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM blocks WHERE block_hash = @hash";
                    cmd.Parameters.AddWithValue("@hash", oldHashHex);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO blocks (block_hash, header_data) VALUES (@hash, @data)";
                    cmd.Parameters.AddWithValue("@hash", newHashHex);
                    cmd.Parameters.AddWithValue("@data", headerData);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE block_numbers SET block_hash = @hash WHERE block_number = @num";
                    cmd.Parameters.AddWithValue("@hash", newHashHex);
                    cmd.Parameters.AddWithValue("@num", blockNum);
                    cmd.ExecuteNonQuery();
                }

                ExecuteSql($"RELEASE SAVEPOINT {sp}");
            }
            catch
            {
                ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                ExecuteSql($"RELEASE SAVEPOINT {sp}");
                throw;
            }

            return Task.CompletedTask;
        }

        public Task DeleteByNumberAsync(BigInteger blockNumber)
        {
            var blockNum = (long)blockNumber;

            var sp = NextSavepoint();
            ExecuteSql($"SAVEPOINT {sp}");

            try
            {
                string hashHex;
                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT block_hash FROM block_numbers WHERE block_number = @num";
                    cmd.Parameters.AddWithValue("@num", blockNum);
                    hashHex = cmd.ExecuteScalar() as string;
                }

                if (hashHex != null)
                {
                    using (var cmd = _manager.Connection.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM blocks WHERE block_hash = @hash";
                        cmd.Parameters.AddWithValue("@hash", hashHex);
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = _manager.Connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM block_numbers WHERE block_number = @num";
                    cmd.Parameters.AddWithValue("@num", blockNum);
                    cmd.ExecuteNonQuery();
                }

                var currentHeight = GetHeightInternal();
                if (blockNum == currentHeight)
                {
                    var newHeight = blockNum - 1;
                    var heightBytes = ((BigInteger)newHeight).ToByteArray(isUnsigned: true, isBigEndian: true);
                    using var cmd = _manager.Connection.CreateCommand();
                    cmd.CommandText = "INSERT OR REPLACE INTO metadata (key, value) VALUES ('height', @val)";
                    cmd.Parameters.AddWithValue("@val", heightBytes);
                    cmd.ExecuteNonQuery();
                }

                ExecuteSql($"RELEASE SAVEPOINT {sp}");
            }
            catch
            {
                ExecuteSql($"ROLLBACK TO SAVEPOINT {sp}");
                ExecuteSql($"RELEASE SAVEPOINT {sp}");
                throw;
            }

            return Task.CompletedTask;
        }

        private string NextSavepoint() => $"bs_{Interlocked.Increment(ref _savepointCounter)}";

        private void ExecuteSql(string sql)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
