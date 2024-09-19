using System.Numerics;
using Nethereum.Mud.TableRepository;
using Microsoft.EntityFrameworkCore;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Mud.Repositories.EntityFramework;

namespace Nethereum.Mud.Repositories.Postgres
{
    public class MudPostgresStoreRecordsTableRepository : MudEFTableRepository<MudPostgresStoreRecordsDbContext>
    {
        public MudPostgresStoreRecordsTableRepository(MudPostgresStoreRecordsDbContext context) : base(context)
        {

        }

        public override async Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex)
        {
            var tableIdBytes = tableIdHex.HexToByteArray();
            var keyBytes = keyHex.HexToByteArray();

            return await Context.StoredRecords
                .AsNoTracking()  // No tracking for read-only query
                .FirstOrDefaultAsync(r => r.TableIdBytes == tableIdBytes && r.KeyBytes == keyBytes);
        }

        // Optimized GetRecordsAsync using AsNoTracking and batch processing
        public override async Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex)
        {
            var tableIdBytes = tableIdHex.HexToByteArray();  // Convert hex to byte array
            const int batchSize = 1000;
            var totalRecords = await Context.StoredRecords.CountAsync(r => r.TableIdBytes == tableIdBytes && !r.IsDeleted);
            var encodedRecords = new List<EncodedTableRecord>();

            for (int i = 0; i < totalRecords; i += batchSize)
            {
                var batch = await Context.StoredRecords
                    .AsNoTracking()
                    .Where(r => r.TableIdBytes == tableIdBytes && !r.IsDeleted)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var storedRecord in batch)
                {
                    encodedRecords.Add(new EncodedTableRecord
                    {
                        TableId = storedRecord.TableIdBytes,
                        Key = storedRecord.KeyBytes.SplitBytes(),
                        EncodedValues = storedRecord
                    });
                }
            }

            return encodedRecords;
        }

        public override async Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var fullKey = ByteUtil.Merge(key.ToArray());
            var storedRecord = await Context.StoredRecords.FirstOrDefaultAsync(r => r.TableIdBytes == tableId && r.KeyBytes == fullKey);
            if (storedRecord != null)
            {
                storedRecord.IsDeleted = true;
                storedRecord.AddressBytes = address.HexToByteArray();
                storedRecord.BlockNumber = blockNumber;
                storedRecord.LogIndex = logIndex;
                Context.StoredRecords.Update(storedRecord);

                await Context.SaveChangesAsync();
            }
        }

        // Reuse of the record creation/updating logic
        protected override async Task<StoredRecord> CreateOrUpdateRecordAsync(byte[] tableId, List<byte[]> key, string address, BigInteger? blockNumber, int? logIndex)
        {
            var fullKey = ByteUtil.Merge(key.ToArray());
            var storedRecord = await Context.StoredRecords.FirstOrDefaultAsync(r => r.TableIdBytes == tableId && r.KeyBytes == fullKey);

            if (storedRecord == null)
            {
                storedRecord = new StoredRecord
                {
                    TableIdBytes = tableId,
                    KeyBytes = fullKey,
                    EncodedLengths = new byte[0],
                    DynamicData = new byte[0],
                    StaticData = new byte[0]
                };

                // Set key0-key3 byte arrays if present
                SetKeyBytes(storedRecord, key);
            }

            storedRecord.AddressBytes = address.HexToByteArray();
            storedRecord.BlockNumber = blockNumber;
            storedRecord.LogIndex = logIndex;
            storedRecord.IsDeleted = false;

            if (Context.Entry(storedRecord).State == EntityState.Detached)
            {
                await Context.StoredRecords.AddAsync(storedRecord);
            }
            else
            {
                Context.StoredRecords.Update(storedRecord);
            }

            return storedRecord;
        }

        public override async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex)
        {
            var tableIdBytes = tableIdHex.HexToByteArray();
            const int batchSize = 1000;
            var totalRecords = await Context.StoredRecords.CountAsync(r => r.TableIdBytes == tableIdBytes && !r.IsDeleted);
            var result = new List<TTableRecord>();

            for (int i = 0; i < totalRecords; i += batchSize)
            {
                var storedRecords = await Context.StoredRecords
                    .AsNoTracking()  // No tracking for better memory performance
                    .Where(r => r.TableIdBytes == tableIdBytes && !r.IsDeleted)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var storedRecord in storedRecords)
                {
                    var tableRecord = new TTableRecord();
                    tableRecord.DecodeValues(storedRecord);

                    if (tableRecord is ITableRecord tableRecordKey)
                    {
                        tableRecordKey.DecodeKey(storedRecord.KeyBytes.SplitBytes());
                    }

                    result.Add(tableRecord);
                }
            }

            return result;
        }

        public override Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate)
        {
            var builder = new MudPostgresStoreRecordsSqlByteaPredicateBuilder();
            var sqlPredicate = builder.BuildSql(predicate);

            string sqlQuery = $"SELECT * FROM storedrecords WHERE {sqlPredicate.Sql}";

            // Pass the parameters dynamically
            return Context.StoredRecords.FromSqlRaw(sqlQuery, sqlPredicate.GetParameterValues()).ToListAsync();
        }

        public async override Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate)
        {
            var storedRecords = await GetRecordsAsync(predicate);
            var encodedTableRecords = storedRecords.Select(storedRecord => new EncodedTableRecord
            {
                TableId = storedRecord.TableId.HexToByteArray(),
                Key = ConvertKeyFromCombinedHex(storedRecord.Key),
                EncodedValues = storedRecord
            });
            var result = new List<TTableRecord>();
            foreach (var encodedTableRecord in encodedTableRecords)
            {
                var tableRecord = new TTableRecord();
                tableRecord.DecodeValues(encodedTableRecord.EncodedValues);

                if (tableRecord is ITableRecord tableRecordKey)
                {
                    tableRecordKey.DecodeKey(encodedTableRecord.Key);
                }

                result.Add(tableRecord);
            }
            return result;

        }

    }
}
