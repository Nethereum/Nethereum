using Microsoft.EntityFrameworkCore;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.TableRepository;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Mud.Repositories.EntityFramework
{

    public abstract class MudEFTableRepository<TDbContext> : TableRepositoryBase, ITableRepository where TDbContext : DbContext, IMudStoreRecordsDbSets
    {
        protected readonly TDbContext Context;

        public MudEFTableRepository(TDbContext context)
        {
            Context = context;            
        }

        // Optimized GetRecordAsync using AsNoTracking
        public override async Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex)
        {
            return await Context.StoredRecords
                .AsNoTracking()  // No tracking for read-only query
                .FirstOrDefaultAsync(r => r.TableId == tableIdHex && r.Key == keyHex);
        }

        // Optimized GetRecordsAsync using AsNoTracking and batch processing
        public override async Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex)
        {
            const int batchSize = 1000;
            var totalRecords = await Context.StoredRecords.CountAsync(r => r.TableId == tableIdHex && !r.IsDeleted);
            var encodedRecords = new List<EncodedTableRecord>();

            for (int i = 0; i < totalRecords; i += batchSize)
            {
                var batch = await Context.StoredRecords
                    .AsNoTracking()
                    .Where(r => r.TableId == tableIdHex && !r.IsDeleted)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var storedRecord in batch)
                {
                    encodedRecords.Add(new EncodedTableRecord
                    {
                        TableId = storedRecord.TableId.HexToByteArray(),
                        Key = ConvertKeyFromCombinedHex(storedRecord.Key),
                        EncodedValues = storedRecord
                    });
                }
            }

            return encodedRecords;
        }

        // Optimized SetRecordAsync with change tracker clearing and batch updates
        public override async Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            Context.ChangeTracker.AutoDetectChangesEnabled = false;

            var storedRecord = await CreateOrUpdateRecordAsync(tableId, key, address, blockNumber, logIndex);
            storedRecord.StaticData = encodedValues.StaticData;
            storedRecord.EncodedLengths = encodedValues.EncodedLengths;
            storedRecord.DynamicData = encodedValues.DynamicData;

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();  // Clear tracking to avoid memory bloat
            Context.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        // Optimized SetRecordAsync for manual byte[] handling
        public override async Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            Context.ChangeTracker.AutoDetectChangesEnabled = false;

            var storedRecord = await CreateOrUpdateRecordAsync(tableId, key, address, blockNumber, logIndex);
            storedRecord.StaticData = staticData;
            storedRecord.EncodedLengths = encodedLengths;
            storedRecord.DynamicData = dynamicData;

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();  // Clear tracking
            Context.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        // Optimized method for handling large data with batching
        public virtual async Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            Context.ChangeTracker.AutoDetectChangesEnabled = false;

            var storedRecord = await CreateOrUpdateRecordAsync(tableId, key, address, blockNumber, logIndex);
            storedRecord.StaticData = SpliceBytes(storedRecord.StaticData, (int)start, newData.Length, newData);

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            Context.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        public virtual async Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            Context.ChangeTracker.AutoDetectChangesEnabled = false;

            var storedRecord = await CreateOrUpdateRecordAsync(tableId, key, address, blockNumber, logIndex);
            storedRecord.DynamicData = SpliceBytes(storedRecord.DynamicData, (int)start, (int)deleteCount, newData);
            storedRecord.EncodedLengths = encodedLengths;

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            Context.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        public virtual async Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            var storedRecord = await Context.StoredRecords.FirstOrDefaultAsync(r => r.TableId == tableIdHex && r.Key == keyHex);
            if (storedRecord != null)
            {
                storedRecord.IsDeleted = true;
                storedRecord.Address = address;
                storedRecord.BlockNumber = blockNumber;
                storedRecord.LogIndex = logIndex;
                Context.StoredRecords.Update(storedRecord);

                await Context.SaveChangesAsync();
            }
        }

        // Reuse of the record creation/updating logic
        protected  virtual async Task<StoredRecord> CreateOrUpdateRecordAsync(byte[] tableId, List<byte[]> key, string address, BigInteger? blockNumber, int? logIndex)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();
            var storedRecord = await GetRecordAsync(tableIdHex, keyHex);

            if (storedRecord == null)
            {
                storedRecord = new StoredRecord
                {
                    TableId = tableIdHex,
                    Key = keyHex,
                    EncodedLengths = new byte[0],
                    DynamicData = new byte[0],
                    StaticData = new byte[0]
                };

                // Set key0-key3
                SetKeyBytes(storedRecord, key);
            }

            storedRecord.Address = address;
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
            const int batchSize = 1000;
            var totalRecords = await Context.StoredRecords.CountAsync(r => r.TableId == tableIdHex && !r.IsDeleted);
            var result = new List<TTableRecord>();

            for (int i = 0; i < totalRecords; i += batchSize)
            {
                var storedRecords = await Context.StoredRecords
                    .AsNoTracking()  // No tracking for better memory performance
                    .Where(r => r.TableId == tableIdHex && !r.IsDeleted)
                    .Skip(i)
                    .Take(batchSize)
                    .ToListAsync();

                foreach (var storedRecord in storedRecords)
                {
                    var tableRecord = new TTableRecord();
                    tableRecord.DecodeValues(storedRecord);

                    if (tableRecord is ITableRecord tableRecordKey)
                    {
                        tableRecordKey.DecodeKey(ConvertKeyFromCombinedHex(storedRecord.Key));
                    }

                    result.Add(tableRecord);
                }
            }

            return result;
        }

        public abstract Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate);
        public abstract Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate) where TTableRecord : ITableRecord, new();
    }

}
