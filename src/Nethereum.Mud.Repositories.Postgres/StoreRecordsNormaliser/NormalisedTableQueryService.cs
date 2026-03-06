using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Mud.TableRepository;
using Npgsql;

namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{
    public class NormalisedTableQueryService : INormalisedTableQueryService
    {
        private readonly NpgsqlConnection _connection;
        private readonly ILogger _logger;

        public NormalisedTableQueryService(NpgsqlConnection connection, ILogger<NormalisedTableQueryService> logger = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger = logger;
        }

        public async Task<List<Dictionary<string, object>>> QueryAsync(
            string tableName, TablePredicate predicate = null, int pageSize = 100, int page = 0)
        {
            var parameters = new List<NpgsqlParameter>();
            var sql = new StringBuilder($"SELECT * FROM {SanitizeIdentifier(tableName)}");

            if (predicate != null && (predicate.Conditions.Count > 0 || predicate.Groups.Count > 0))
            {
                sql.Append(" WHERE ");
                sql.Append(BuildWhereClause(predicate, parameters));
            }

            var effectivePageSize = predicate?.PageSize ?? pageSize;
            var effectivePage = predicate?.Page ?? page;

            if (!string.IsNullOrEmpty(predicate?.OrderByField))
            {
                sql.Append($" ORDER BY {SanitizeIdentifier(predicate.OrderByField)}");
                if (predicate.OrderByDescending)
                    sql.Append(" DESC");
            }

            sql.Append($" LIMIT {effectivePageSize} OFFSET {effectivePage * effectivePageSize}");

            return await ExecuteQueryAsync(sql.ToString(), parameters);
        }

        public async Task<int> CountAsync(string tableName, TablePredicate predicate = null)
        {
            var parameters = new List<NpgsqlParameter>();
            var sql = new StringBuilder($"SELECT COUNT(*) FROM {SanitizeIdentifier(tableName)}");

            if (predicate != null && (predicate.Conditions.Count > 0 || predicate.Groups.Count > 0))
            {
                sql.Append(" WHERE ");
                sql.Append(BuildWhereClause(predicate, parameters));
            }

            try
            {
                await _connection.OpenAsync();
                using var command = new NpgsqlCommand(sql.ToString(), _connection);
                foreach (var param in parameters)
                    command.Parameters.Add(param);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<List<string>> GetAvailableTablesAsync(string prefix = "w_")
        {
            var tables = new List<string>();
            try
            {
                await _connection.OpenAsync();
                using var command = new NpgsqlCommand(
                    "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE @prefix ORDER BY tablename",
                    _connection);
                command.Parameters.AddWithValue("prefix", prefix + "%");

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }
            finally
            {
                await _connection.CloseAsync();
            }

            return tables;
        }

        public async Task<List<NormalisedColumnInfo>> GetTableColumnsAsync(string tableName)
        {
            var columns = new List<NormalisedColumnInfo>();
            try
            {
                await _connection.OpenAsync();
                using var command = new NpgsqlCommand(
                    "SELECT column_name, data_type, ordinal_position FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @tableName ORDER BY ordinal_position",
                    _connection);
                command.Parameters.AddWithValue("tableName", tableName);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(new NormalisedColumnInfo
                    {
                        Name = reader.GetString(0),
                        DataType = reader.GetString(1),
                        OrdinalPosition = reader.GetInt32(2)
                    });
                }
            }
            finally
            {
                await _connection.CloseAsync();
            }

            return columns;
        }

        private string BuildWhereClause(TablePredicate predicate, List<NpgsqlParameter> parameters)
        {
            var parts = new List<string>();

            foreach (var condition in predicate.Conditions)
            {
                var paramName = $"@p{parameters.Count}";
                var columnName = SanitizeIdentifier(condition.IsValueField
                    ? condition.Name.ToLowerInvariant()
                    : condition.Key);

                var op = condition.ComparisonOperator ?? "=";
                parts.Add($"{columnName} {op} {paramName}");

                var paramValue = ConvertConditionValue(condition);
                parameters.Add(new NpgsqlParameter(paramName, paramValue));
            }

            foreach (var group in predicate.Groups)
            {
                var groupClause = BuildWhereClause(group, parameters);
                if (!string.IsNullOrEmpty(groupClause))
                    parts.Add($"({groupClause})");
            }

            if (parts.Count == 0) return "";

            var separator = $" {predicate.CombineOperator ?? "AND"} ";
            return string.Join(separator, parts);
        }

        private object ConvertConditionValue(KeyValueOperator condition)
        {
            if (condition.RawValue != null)
                return ConvertToNativeValue(condition.RawValue, condition.AbiType);

            if (condition.HexValue == null)
                return DBNull.Value;

            return condition.HexValue;
        }

        private object ConvertToNativeValue(object value, string abiType)
        {
            if (value == null) return DBNull.Value;

            return abiType switch
            {
                "address" => value.ToString(),
                "bool" => Convert.ToBoolean(value),
                "string" => value.ToString(),
                "uint256" or "int256" or "uint128" or "int128" or "uint64" => ConvertToDecimal(value),
                _ => value
            };
        }

        private object ConvertToDecimal(object value)
        {
            if (value is BigInteger bigInt)
            {
                if (bigInt <= (BigInteger)decimal.MaxValue && bigInt >= (BigInteger)decimal.MinValue)
                    return (decimal)bigInt;
                return bigInt.ToString();
            }
            if (value is string s)
            {
                if (long.TryParse(s, out var l)) return l;
                if (decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
                if (BigInteger.TryParse(s, out var bi))
                {
                    if (bi <= (BigInteger)decimal.MaxValue && bi >= (BigInteger)decimal.MinValue)
                        return (decimal)bi;
                    return bi.ToString();
                }
            }
            return value;
        }

        private async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sql, List<NpgsqlParameter> parameters)
        {
            var results = new List<Dictionary<string, object>>();
            try
            {
                await _connection.OpenAsync();
                using var command = new NpgsqlCommand(sql, _connection);
                foreach (var param in parameters)
                    command.Parameters.Add(param);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    results.Add(row);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing normalised table query: {Sql}", sql);
                throw;
            }
            finally
            {
                await _connection.CloseAsync();
            }

            return results;
        }

        private static string SanitizeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier cannot be null or empty");

            var sanitized = new string(identifier.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (sanitized.Length == 0)
                throw new ArgumentException($"Invalid identifier: {identifier}");

            return sanitized;
        }
    }

}
