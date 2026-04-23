using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteTransactionStore : ITransactionStore
    {
        private readonly SqliteStorageManager _manager;
        private readonly IBlockEncodingProvider _provider;

        public SqliteTransactionStore(SqliteStorageManager manager, IBlockEncodingProvider provider = null)
        {
            _manager = manager;
            _provider = provider ?? RlpBlockEncodingProvider.Instance;
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<ISignedTransaction>(null);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT tx_data FROM transactions WHERE tx_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", txHash.ToHex());

            var data = cmd.ExecuteScalar() as byte[];
            if (data == null) return Task.FromResult<ISignedTransaction>(null);

            var tx = _provider.DecodeTransaction(data);
            return Task.FromResult(tx);
        }

        public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<ISignedTransaction>();
            if (blockHash == null) return Task.FromResult(result);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT tx_data FROM transactions WHERE block_hash = @hash ORDER BY tx_index";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["tx_data"];
                var tx = _provider.DecodeTransaction(data);
                if (tx != null) result.Add(tx);
            }

            return Task.FromResult(result);
        }

        public Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<byte[]>();
            if (blockHash == null) return Task.FromResult(result);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT tx_hash FROM transactions WHERE block_hash = @hash ORDER BY tx_index";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var hash = ((string)reader["tx_hash"]).HexToByteArray();
                result.Add(hash);
            }

            return Task.FromResult(result);
        }

        public Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var result = new List<ISignedTransaction>();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT tx_data FROM transactions WHERE block_number = @num ORDER BY tx_index";
            cmd.Parameters.AddWithValue("@num", (long)blockNumber);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["tx_data"];
                var tx = _provider.DecodeTransaction(data);
                if (tx != null) result.Add(tx);
            }

            return Task.FromResult(result);
        }

        public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber)
        {
            if (tx == null || blockHash == null) return Task.CompletedTask;

            var txHash = tx.Hash.ToHex();
            var txBytes = _provider.EncodeTransaction(tx);
            var blockHashHex = blockHash.ToHex();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO transactions (tx_hash, tx_data, block_hash, block_number, tx_index)
                                VALUES (@txHash, @txData, @blockHash, @blockNumber, @txIndex)";
            cmd.Parameters.AddWithValue("@txHash", txHash);
            cmd.Parameters.AddWithValue("@txData", txBytes);
            cmd.Parameters.AddWithValue("@blockHash", blockHashHex);
            cmd.Parameters.AddWithValue("@blockNumber", (long)blockNumber);
            cmd.Parameters.AddWithValue("@txIndex", txIndex);
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        public Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<TransactionLocation>(null);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT block_hash, block_number, tx_index FROM transactions WHERE tx_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", txHash.ToHex());

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return Task.FromResult<TransactionLocation>(null);

            return Task.FromResult(new TransactionLocation
            {
                BlockHash = ((string)reader["block_hash"]).HexToByteArray(),
                BlockNumber = (long)reader["block_number"],
                TransactionIndex = (int)(long)reader["tx_index"]
            });
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM transactions WHERE block_number = @num";
            cmd.Parameters.AddWithValue("@num", (long)blockNumber);
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

    }
}
