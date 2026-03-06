using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevChain.Storage.Sqlite
{
    public class SqliteReceiptStore : IReceiptStore
    {
        private readonly SqliteStorageManager _manager;

        public SqliteReceiptStore(SqliteStorageManager manager)
        {
            _manager = manager;
        }

        public Task<Receipt> GetByTxHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<Receipt>(null);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT receipt_data FROM receipts WHERE tx_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", txHash.ToHex());

            var data = cmd.ExecuteScalar() as byte[];
            if (data == null) return Task.FromResult<Receipt>(null);

            return Task.FromResult(ReceiptEncoder.Current.Decode(data));
        }

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<ReceiptInfo>(null);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT receipt_data, block_hash, block_number, tx_index, gas_used, contract_address, effective_gas_price FROM receipts WHERE tx_hash = @hash";
            cmd.Parameters.AddWithValue("@hash", txHash.ToHex());

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return Task.FromResult<ReceiptInfo>(null);

            var receiptData = (byte[])reader["receipt_data"];
            var receipt = ReceiptEncoder.Current.Decode(receiptData);

            var info = new ReceiptInfo
            {
                Receipt = receipt,
                TxHash = txHash,
                BlockHash = ((string)reader["block_hash"]).HexToByteArray(),
                BlockNumber = (long)reader["block_number"],
                TransactionIndex = (int)(long)reader["tx_index"],
                GasUsed = DecodeBigInteger(reader["gas_used"]),
                ContractAddress = reader["contract_address"] as string,
                EffectiveGasPrice = DecodeBigInteger(reader["effective_gas_price"])
            };

            return Task.FromResult(info);
        }

        public Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<Receipt>();
            if (blockHash == null) return Task.FromResult(result);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT receipt_data FROM receipts WHERE block_hash = @hash ORDER BY tx_index";
            cmd.Parameters.AddWithValue("@hash", blockHash.ToHex());

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["receipt_data"];
                var receipt = ReceiptEncoder.Current.Decode(data);
                if (receipt != null) result.Add(receipt);
            }

            return Task.FromResult(result);
        }

        public Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var result = new List<Receipt>();

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "SELECT receipt_data FROM receipts WHERE block_number = @num ORDER BY tx_index";
            cmd.Parameters.AddWithValue("@num", (long)blockNumber);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var data = (byte[])reader["receipt_data"];
                var receipt = ReceiptEncoder.Current.Decode(data);
                if (receipt != null) result.Add(receipt);
            }

            return Task.FromResult(result);
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex, BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
        {
            if (receipt == null || txHash == null) return Task.CompletedTask;

            var receiptData = ReceiptEncoder.Current.Encode(receipt);

            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO receipts
                (tx_hash, receipt_data, block_hash, block_number, tx_index, gas_used, contract_address, effective_gas_price)
                VALUES (@txHash, @receiptData, @blockHash, @blockNumber, @txIndex, @gasUsed, @contractAddress, @effectiveGasPrice)";
            cmd.Parameters.AddWithValue("@txHash", txHash.ToHex());
            cmd.Parameters.AddWithValue("@receiptData", receiptData);
            cmd.Parameters.AddWithValue("@blockHash", blockHash.ToHex());
            cmd.Parameters.AddWithValue("@blockNumber", (long)blockNumber);
            cmd.Parameters.AddWithValue("@txIndex", txIndex);
            cmd.Parameters.AddWithValue("@gasUsed", EncodeBigInteger(gasUsed));
            cmd.Parameters.AddWithValue("@contractAddress", (object)contractAddress ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@effectiveGasPrice", EncodeBigInteger(effectiveGasPrice));
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            using var cmd = _manager.Connection.CreateCommand();
            cmd.CommandText = "DELETE FROM receipts WHERE block_number = @num";
            cmd.Parameters.AddWithValue("@num", (long)blockNumber);
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        private static byte[] EncodeBigInteger(BigInteger value)
        {
            return value.ToByteArray(isUnsigned: true, isBigEndian: true);
        }

        private static BigInteger DecodeBigInteger(object value)
        {
            if (value == null || value is System.DBNull) return BigInteger.Zero;
            var bytes = (byte[])value;
            if (bytes.Length == 0) return BigInteger.Zero;
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }
    }
}
