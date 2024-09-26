using Npgsql;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{
    public class MudPostgresNormaliserProgressService : IMudNormaliserProgressService
    {
        private readonly NpgsqlConnection _connection;
        private readonly ILogger _logger;

        public MudPostgresNormaliserProgressService(NpgsqlConnection connection, ILogger logger)
        {
            _connection = connection;
            _logger = logger;
        }

        public async Task CreateProgressTableIfNotExistsAsync()
        {
            var sql = @"
            CREATE TABLE IF NOT EXISTS __normaliser_progress (
                id SERIAL PRIMARY KEY,
                last_row_id BIGINT NOT NULL,
                last_block_number NUMERIC(78,0) NOT NULL
            );
            INSERT INTO __normaliser_progress (id, last_row_id, last_block_number)
            VALUES (1, 0, 0)
            ON CONFLICT DO NOTHING;
        ";

            try
            {
                await _connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, _connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating progress table.");
                throw;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task UpsertProgressAsync(NormaliserProgressInfo progressInfo)
        {
            var sql = @"
            UPDATE __normaliser_progress 
            SET last_row_id = @rowId, last_block_number = @blockNumber
            WHERE id = 1;
        ";

            try
            {
                await _connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("rowId", progressInfo.RowId);
                    command.Parameters.AddWithValue("blockNumber", progressInfo.BlockNumber);
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating progress.");
                throw;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<NormaliserProgressInfo> GetProgressAsync()
        {
            var sql = "SELECT last_row_id, last_block_number FROM __normaliser_progress WHERE id = 1;";

            try
            {
                await _connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, _connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var rowId = reader.GetInt64(0);
                            var blockNumber = (BigInteger)reader.GetDecimal(1);
                            return new NormaliserProgressInfo { RowId = rowId, BlockNumber = blockNumber };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving progress.");
                throw;
            }
            finally
            {
                await _connection.CloseAsync();
            }

            return new NormaliserProgressInfo { RowId = 0, BlockNumber = 0 }; // Default if no progress found
        }

        // Clear the progress by resetting RowId and BlockNumber to 0
        public async Task ClearProgressAsync()
        {
            var sql = @"
            UPDATE __normaliser_progress 
            SET last_row_id = 0, last_block_number = 0
            WHERE id = 1;
        ";

            try
            {
                await _connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, _connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
                _logger.LogInformation("Progress has been cleared.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing progress.");
                throw;
            }
            finally
            {
                await _connection.CloseAsync();
            }
        }
    }
}

