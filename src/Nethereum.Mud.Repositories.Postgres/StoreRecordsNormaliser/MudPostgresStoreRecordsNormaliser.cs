using System.Threading.Tasks;
using Npgsql;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Mud.Contracts.Store.Tables;
using System;
using Microsoft.Extensions.Logging;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.Mud;
using System.Collections;
using Nethereum.Hex.HexConvertors.Extensions;
namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{

    public class MudPostgresStoreRecordsNormaliser
    {
        private readonly NpgsqlConnection _connection;
        private readonly ConcurrentDictionary<string, TableSchema> _tableSchemaCache = new();
        private readonly StoreNamespace _storeNamespace;
        private readonly ILogger _logger;

        public MudPostgresStoreRecordsNormaliser(NpgsqlConnection connection, StoreNamespace storeNamespace, ILogger logger)
        {
            _connection = connection;
            _storeNamespace = storeNamespace;
            _logger = logger;
        }

        public async Task<TableSchema> GetTableSchemaAsync(byte[] tableId)
        {
            var hexTableId = tableId.ToHex();
            // Cache lookup by encoded tableId
            if (_tableSchemaCache.TryGetValue(hexTableId, out var schema))
            {
                return schema;
            }

            // Cache miss, retrieve the schema from blockchain
            var tableRecord = await _storeNamespace.Tables.TablesTableService.GetTableRecordAsync(tableId);
            schema = tableRecord.GetTableSchema();

            // Cache the schema for future access
            _tableSchemaCache.TryAdd(hexTableId, schema);

            await CreateTableIfNotExistsAsync(schema);

            return schema;
        }

        private string GetTableName(TableSchema schema)
        {
            if (string.IsNullOrEmpty(schema.Namespace))
            {
                return schema.GetTableNameTrimmedForResource().ToLowerInvariant();
            }
            return schema.Namespace + "_" + schema.GetTableNameTrimmedForResource().ToLowerInvariant();
        }

        // Create table if not exists, with lowercase table/column names
        public async Task CreateTableIfNotExistsAsync(TableSchema schema)
        {
            var tableNameLower = GetTableName(schema);

            var sql = new StringBuilder($"CREATE TABLE IF NOT EXISTS {tableNameLower} (");

            // Collect columns and primary key columns
            var columns = new List<string>();
            var primaryKeyColumns = new List<string>();

            // Loop through the schema fields and collect columns and keys
            foreach (var field in schema.SchemaKeys.OrderBy(f => f.Order).Concat(schema.SchemaValues.OrderBy(f => f.Order)))
            {
                var columnDefinition = $"{field.Name.ToLowerInvariant()} {ConvertToPostgresType(field.Type)}";
                columns.Add(columnDefinition);

                if (field.IsKey)
                {
                    primaryKeyColumns.Add(field.Name.ToLowerInvariant());
                }
            }

            // Add the id column if it's a singleton table
            if (!primaryKeyColumns.Any())
            {
                sql.Append("id SERIAL PRIMARY KEY, ");
            }

            // Join all columns together
            sql.Append(string.Join(", ", columns));

            // Add the composite primary key (if there are any primary key columns)
            if (primaryKeyColumns.Any())
            {
                sql.Append($", PRIMARY KEY ({string.Join(", ", primaryKeyColumns)})");
            }

            sql.Append(");");

            try
            {
                // Open the connection before performing operations
                _logger.LogInformation($"Creating new table: {tableNameLower}");
                await _connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql.ToString(), _connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating table: {ex.Message}");
                _logger?.LogError($"SQL: {sql}");
                throw new Exception($"Error creating table: {ex.Message}, SQL: {sql}", ex);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task UpsertRecordAsync(EncodedTableRecord encodedTableRecord)
        {
            var schema = await GetTableSchemaAsync(encodedTableRecord.TableId);
            var fieldValues = schema.GetFieldValues(encodedTableRecord);
            await UpsertRecordAsync(schema, fieldValues);
        }

        public async Task UpsertRecordAsync(TableSchema schema, List<FieldValue> fieldValues)
        {
            var tableNameLower = GetTableName(schema); // Ensure the table name uses the correct format
            var columns = new StringBuilder();
            var values = new StringBuilder();
            var updates = new StringBuilder();

            // Handle singleton tables by adding an id column with a value of 1
            if (!schema.SchemaKeys.Any())
            {
                columns.Append("id, ");
                values.Append("1, ");
            }

            foreach (var field in schema.SchemaKeys.OrderBy(f => f.Order).Concat(schema.SchemaValues.OrderBy(f => f.Order)))
            {
                columns.Append($"{field.Name.ToLowerInvariant()}, ");
                values.Append($"@{field.Name.ToLowerInvariant()}, ");
                if (!field.IsKey)
                {
                    updates.Append($"{field.Name.ToLowerInvariant()} = @{field.Name.ToLowerInvariant()}, ");
                }
            }

            if (updates.Length == 0)
            {
                return;
            }

            // Remove the trailing comma and space from columns, values, and updates
           
             columns.Length -= 2;
             values.Length -= 2;
             updates.Length -= 2;

            // Check if the schema has keys (normal tables)
            if (schema.SchemaKeys.Any())
            {
                // Scenario 1: Use ON CONFLICT for normal tables
                var conflictColumns = string.Join(", ", schema.SchemaKeys.Select(k => k.Name.ToLowerInvariant()));
                var sql = $@"
                INSERT INTO {tableNameLower} ({columns})
                VALUES ({values})
                ON CONFLICT ({conflictColumns})
                DO UPDATE SET {updates};";

                await ExecuteSqlWithParametersAsync(sql, fieldValues);
            }
            else
            {
                // Scenario 2: Singleton tables, use id=1
                var sql = $@"
                INSERT INTO {tableNameLower} ({columns})
                VALUES ({values})
                ON CONFLICT (id)
                DO UPDATE SET {updates};";

                await ExecuteSqlWithParametersAsync(sql, fieldValues);
            }
        }

        public async Task DeleteRecordAsync(EncodedTableRecord encodedTableRecord)
        {
            var schema = await GetTableSchemaAsync(encodedTableRecord.TableId);
            var fieldValues = schema.GetFieldValues(encodedTableRecord);
            await DeleteRecordAsync(schema, fieldValues);
        }

        public async Task DeleteRecordAsync(TableSchema schema, List<FieldValue> fieldValues)
        {
            var tableNameLower = GetTableName(schema); // Ensure the table name uses the correct format
            var whereClause = new StringBuilder("WHERE ");

            // Build the WHERE clause using schema keys (this assumes we delete records based on key fields)
            foreach (var field in schema.SchemaKeys.OrderBy(f => f.Order))
            {
                whereClause.Append($"{field.Name.ToLowerInvariant()} = @{field.Name.ToLowerInvariant()} AND ");
            }

            // Remove the trailing " AND " from the whereClause
            whereClause.Length -= 5;

            // Construct the DELETE SQL statement
            var sql = $"DELETE FROM {tableNameLower} {whereClause};";

            // Execute the DELETE SQL query with the appropriate field values
            await ExecuteSqlWithParametersAsync(sql, fieldValues);
        }

        private async Task ExecuteSqlWithParametersAsync(string sql, List<FieldValue> fieldValues)
        {
            try
            {
                await _connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, _connection))
                {
                    foreach (var fieldValue in fieldValues)
                    {
                        var parameterName = fieldValue.Name.ToLowerInvariant();
                        command.Parameters.AddWithValue(parameterName, ConvertToPostgresValue(fieldValue));
                    }
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                var fieldDetails = string.Join(", ", fieldValues.Select(fv => $"{fv.Name.ToLowerInvariant()}: {ConvertToPostgresValue(fv)}"));
                _logger?.LogError(ex, $"Error executing SQL: {ex.Message}");
                _logger?.LogError($"SQL: {sql}");
                _logger?.LogError($"Field values: {fieldDetails}");
                throw new Exception($"Error executing SQL: {ex.Message}, SQL: {sql}, Field values: {fieldDetails}", ex);
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        private string ConvertToPostgresType(string abiType)
        {
            if (abiType.EndsWith("[]"))
            {
                return "BYTEA"; // Arrays stored as binary
            }

            return abiType switch
            {
                "address" => "TEXT",
                "bool" => "BOOLEAN",
                "string" => "TEXT",
                "uint8" => "SMALLINT",         // Unsigned 8-bit integer, SMALLINT is sufficient
                "uint16" => "INTEGER",         // Unsigned 16-bit integer, use INTEGER for stricter validation
                "uint32" => "BIGINT",          // Unsigned 32-bit integer
                "uint64" => "NUMERIC(20,0)",   // Unsigned 64-bit integer, needs NUMERIC to cover full range
                "uint128" => "NUMERIC(38,0)",  // Unsigned 128-bit integer
                "uint256" => "NUMERIC(78,0)",  // Unsigned 256-bit integer
                "int8" => "SMALLINT",          // Signed 8-bit integer
                "int16" => "SMALLINT",         // Signed 16-bit integer
                "int32" => "INTEGER",          // Signed 32-bit integer
                "int64" => "BIGINT",           // Signed 64-bit integer
                "int128" => "NUMERIC(38,0)",   // Signed 128-bit integer
                "int256" => "NUMERIC(78,0)",   // Signed 256-bit integer
                "bytes" => "BYTEA",            // Byte arrays
                "bytes32" => "BYTEA",          // Fixed byte arrays
                _ => "TEXT"                    // Default to TEXT for unsupported types
            };
        }

        // Convert FieldValue to the appropriate PostgreSQL value, including array conversion to bytes
        private object ConvertToPostgresValue(FieldValue fieldValue)
        {
            if (fieldValue.Value == null)
            {
                return DBNull.Value; 
            }

            return fieldValue.ABIType.Name switch
            {
                "address" => fieldValue.Value?.ToString(),
                "bool" => fieldValue.Value?.ToString().ToLower() == "true",
                "string" => fieldValue.Value?.ToString(),
                "uint256" or "int256" => ConvertBigIntegerToDecimal(fieldValue.Value),
                "bytes" or "bytes32" => (byte[])fieldValue.Value,
                var arrayType when arrayType.EndsWith("[]") => EncodeArrayToBytes(fieldValue),  // Convert array to bytes
                _ => fieldValue.Value
            };
        }

        // Convert BigInteger to decimal
        private object ConvertBigIntegerToDecimal(object value)
        {
            if (value is BigInteger bigIntValue)
            {
                try
                {
                    // Attempt to convert to decimal if within range
                    if (bigIntValue <= (BigInteger)decimal.MaxValue && bigIntValue >= (BigInteger)decimal.MinValue)
                    {
                        return (decimal)bigIntValue;
                    }
                    // If the BigInteger exceeds the range of decimal, store as string
                    return bigIntValue.ToString();
                }
                catch (OverflowException ex)
                {
                    // Log overflow error for troubleshooting (optional)
                    _logger?.LogError(ex, "BigInteger value exceeds the range of decimal.");
                    return bigIntValue.ToString();
                }
            }

            return value; // If it's not a BigInteger, return as-is
        }

        // Encode arrays to bytes using the ABI encoder
        private byte[] EncodeArrayToBytes(FieldValue fieldValue)
        {
            try
            {
                var abiType = fieldValue.ABIType;
                var arrayValue = fieldValue.Value;

                if (arrayValue == null)
                {
                    return null;
                }

                if (arrayValue is IList enumerable)
                {
                    if (enumerable.Count == 0)
                    {
                        return Array.Empty<byte>();
                    }
                    var type = fieldValue.ABIType;
                    return type.Encode(fieldValue.Value);
                }
            }
            catch (Exception ex)
            {
                var message = $"Error encoding to array: {fieldValue.Name}, {fieldValue.ABIType}, {fieldValue.Value?.ToString()}";
                _logger.LogError(message);
                throw new Exception(message, ex);
            }

            throw new ArgumentException("Field value is not an array or is not supported for ABI encoding.");
        }
    }
}