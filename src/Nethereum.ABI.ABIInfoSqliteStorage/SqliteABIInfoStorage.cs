using Microsoft.Data.Sqlite;
using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.ABI.ABIInfoSqliteStorage
{
    public class SqliteABIInfoStorage : IABIInfoStorage, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ABIInfoInMemoryStorage _memoryCache = new ABIInfoInMemoryStorage();
        private bool _disposed;

        public SqliteABIInfoStorage(string connectionString)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            InitializeSchema();
        }

        public SqliteABIInfoStorage(string databasePath, bool createIfNotExists = true)
            : this($"Data Source={databasePath}")
        {
        }

        public static SqliteABIInfoStorage CreateInMemory()
        {
            return new SqliteABIInfoStorage("Data Source=:memory:");
        }

        private void InitializeSchema()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ABIInfo (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ChainId INTEGER NOT NULL,
                        ContractAddress TEXT NOT NULL,
                        ContractName TEXT,
                        ABI TEXT NOT NULL,
                        RuntimeBytecode TEXT,
                        RuntimeSourceMap TEXT,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UNIQUE(ChainId, ContractAddress)
                    );

                    CREATE INDEX IF NOT EXISTS IX_ABIInfo_ChainId_Address
                    ON ABIInfo(ChainId, ContractAddress);

                    CREATE TABLE IF NOT EXISTS FunctionSignature (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Signature TEXT NOT NULL UNIQUE,
                        Name TEXT NOT NULL,
                        InputParameters TEXT
                    );

                    CREATE INDEX IF NOT EXISTS IX_FunctionSignature_Signature
                    ON FunctionSignature(Signature);

                    CREATE TABLE IF NOT EXISTS EventSignature (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Signature TEXT NOT NULL UNIQUE,
                        Name TEXT NOT NULL,
                        InputParameters TEXT
                    );

                    CREATE INDEX IF NOT EXISTS IX_EventSignature_Signature
                    ON EventSignature(Signature);
                ";
                cmd.ExecuteNonQuery();
            }
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            if (abiInfo == null) return;

            _memoryCache.AddABIInfo(abiInfo);

            var chainId = abiInfo.ChainId ?? 0;
            var address = abiInfo.Address?.ToLowerInvariant() ?? "";
            var abi = abiInfo.ABI ?? "";
            var now = DateTime.UtcNow.ToString("O");

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO ABIInfo (ChainId, ContractAddress, ContractName, ABI, RuntimeBytecode, RuntimeSourceMap, CreatedAt, UpdatedAt)
                    VALUES (@chainId, @address, @name, @abi, @bytecode, @sourceMap, @now, @now)
                    ON CONFLICT(ChainId, ContractAddress) DO UPDATE SET
                        ContractName = @name,
                        ABI = @abi,
                        RuntimeBytecode = @bytecode,
                        RuntimeSourceMap = @sourceMap,
                        UpdatedAt = @now
                ";
                cmd.Parameters.AddWithValue("@chainId", chainId);
                cmd.Parameters.AddWithValue("@address", address);
                cmd.Parameters.AddWithValue("@name", abiInfo.ContractName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@abi", abi);
                cmd.Parameters.AddWithValue("@bytecode", abiInfo.RuntimeBytecode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@sourceMap", abiInfo.RuntimeSourceMap ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.ExecuteNonQuery();
            }

            IndexSignatures(abiInfo);
        }

        private void IndexSignatures(ABIInfo abiInfo)
        {
            if (abiInfo.ContractABI == null) return;

            if (abiInfo.ContractABI.Functions != null)
            {
                foreach (var function in abiInfo.ContractABI.Functions)
                {
                    IndexFunctionSignature(function);
                }
            }

            if (abiInfo.ContractABI.Events != null)
            {
                foreach (var evt in abiInfo.ContractABI.Events)
                {
                    IndexEventSignature(evt);
                }
            }
        }

        private void IndexFunctionSignature(FunctionABI function)
        {
            if (function == null || string.IsNullOrEmpty(function.Sha3Signature)) return;

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO FunctionSignature (Signature, Name, InputParameters)
                    VALUES (@sig, @name, @params)
                ";
                cmd.Parameters.AddWithValue("@sig", function.Sha3Signature);
                cmd.Parameters.AddWithValue("@name", function.Name ?? "");
                cmd.Parameters.AddWithValue("@params", SerializeParameters(function.InputParameters));
                cmd.ExecuteNonQuery();
            }
        }

        private void IndexEventSignature(EventABI evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.Sha3Signature)) return;

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT OR IGNORE INTO EventSignature (Signature, Name, InputParameters)
                    VALUES (@sig, @name, @params)
                ";
                cmd.Parameters.AddWithValue("@sig", evt.Sha3Signature);
                cmd.Parameters.AddWithValue("@name", evt.Name ?? "");
                cmd.Parameters.AddWithValue("@params", SerializeParameters(evt.InputParameters));
                cmd.ExecuteNonQuery();
            }
        }

        private string SerializeParameters(Parameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0) return "";
            var types = new List<string>();
            foreach (var p in parameters)
            {
                types.Add(p.Type);
            }
            return string.Join(",", types);
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            var cached = _memoryCache.GetABIInfo(chainId, contractAddress);
            if (cached != null) return cached;

            var address = contractAddress?.ToLowerInvariant() ?? "";

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT ContractName, ABI, RuntimeBytecode, RuntimeSourceMap
                    FROM ABIInfo
                    WHERE ChainId = @chainId AND ContractAddress = @address
                ";
                cmd.Parameters.AddWithValue("@chainId", (long)chainId);
                cmd.Parameters.AddWithValue("@address", address);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var contractName = reader.IsDBNull(0) ? null : reader.GetString(0);
                        var abi = reader.GetString(1);
                        var bytecode = reader.IsDBNull(2) ? null : reader.GetString(2);
                        var sourceMap = reader.IsDBNull(3) ? null : reader.GetString(3);

                        var abiInfo = ABIInfo.FromABI(abi, address, contractName, null, (long)chainId);
                        abiInfo.RuntimeBytecode = bytecode;
                        abiInfo.RuntimeSourceMap = sourceMap;

                        _memoryCache.AddABIInfo(abiInfo);
                        return abiInfo;
                    }
                }
            }

            return null;
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            var result = _memoryCache.FindFunctionABI(chainId, contractAddress, signature);
            if (result != null) return result;

            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                return _memoryCache.FindFunctionABI(chainId, contractAddress, signature);
            }

            return null;
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            var result = _memoryCache.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
            if (result != null) return result;

            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                return _memoryCache.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
            }

            return null;
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            var result = _memoryCache.FindEventABI(chainId, contractAddress, signature);
            if (result != null) return result;

            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                return _memoryCache.FindEventABI(chainId, contractAddress, signature);
            }

            return null;
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            var result = _memoryCache.FindErrorABI(chainId, contractAddress, signature);
            if (result != null) return result;

            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                return _memoryCache.FindErrorABI(chainId, contractAddress, signature);
            }

            return null;
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            return _memoryCache.FindFunctionABI(signature);
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            return _memoryCache.FindFunctionABIFromInputData(inputData);
        }

        public List<EventABI> FindEventABI(string signature)
        {
            return _memoryCache.FindEventABI(signature);
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            return _memoryCache.FindErrorABI(signature);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}
