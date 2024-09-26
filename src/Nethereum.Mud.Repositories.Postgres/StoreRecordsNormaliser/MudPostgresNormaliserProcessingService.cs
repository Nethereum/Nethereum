
using Microsoft.Extensions.Logging;
using Nethereum.Mud.Repositories.Postgres;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Web3;
using Npgsql;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{
    public class MudPostgresNormaliserProcessingService
    {
        private readonly MudPostgresStoreRecordsTableRepository _mudPostgresStoreRecordsTableRepository;
        private readonly NpgsqlConnection _connection;
        private readonly ILogger<MudPostgresNormaliserProcessingService> _logger;

        public string RpcUrl { get; set; }
        public string Address { get; set; }
        public int PageSize { get; set; } = 1000;

        public MudPostgresNormaliserProcessingService(
            MudPostgresStoreRecordsTableRepository mudPostgresStoreRecordsTableRepository,
            NpgsqlConnection connection,
            ILogger<MudPostgresNormaliserProcessingService> logger)
        {
            _mudPostgresStoreRecordsTableRepository = mudPostgresStoreRecordsTableRepository;
            _connection = connection;
            _logger = logger;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var progressService = new MudPostgresNormaliserProgressService(_connection, _logger);
           
           

            _logger.LogInformation("Starting processing service");

            var storeNamespace = new StoreNamespace(new Web3.Web3(RpcUrl), Address);
            var postgresNormaliser = new MudPostgresStoreRecordsNormaliser(_connection, storeNamespace, _logger);

            // Create progress table if it doesn't exist
            await progressService.CreateProgressTableIfNotExistsAsync();

            // Get the last processed progress
            var progressInfo = await progressService.GetProgressAsync();
            long lastProcessedRowId = progressInfo.RowId;
            BigInteger startingBlockNumber = progressInfo.BlockNumber;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Fetch records based on BlockNumber and RowId
                    var records = await _mudPostgresStoreRecordsTableRepository
                        .GetStoredRecordsGreaterThanBlockNumberAsync(PageSize, startingBlockNumber, lastProcessedRowId);

                    if (records != null && records.Records != null && records.Records.Count > 0)
                    {
                        foreach (var record in records.Records)
                        {
                            var encodedRecord = new EncodedTableRecord
                            {
                                TableId = record.TableIdBytes,
                                Key = record.KeyBytes.SplitBytes(),
                                EncodedValues = record
                            };

                            if (record.IsDeleted)
                            {
                                await postgresNormaliser.DeleteRecordAsync(encodedRecord);
                            }
                            else
                            {
                                await postgresNormaliser.UpsertRecordAsync(encodedRecord);
                            }
                        }

                        // Update block number and row id to continue processing
                        startingBlockNumber = records.LastBlockNumber.Value;
                        lastProcessedRowId = records.LastRowId.Value;

                        // Update the progress table after processing
                        await progressService.UpsertProgressAsync(new NormaliserProgressInfo
                        {
                            RowId = lastProcessedRowId,
                            BlockNumber = startingBlockNumber
                        });

                        _logger.LogInformation($"Last BlockNumber processed: {startingBlockNumber}, RowId: {lastProcessedRowId}, continue processing.");
                    }
                    else
                    {
                        _logger.LogInformation($"No records found, waiting for more records. Last BlockNumber processed: {startingBlockNumber}, RowId: {lastProcessedRowId}");
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during processing");
                }
            }
        }
    }
}
