using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Mud.TableRepository
{
    public class InMemoryChangeSet
    {
        private readonly Dictionary<string, Dictionary<string, StoredRecord>> _upserted = new();
        private readonly Dictionary<string, Dictionary<string, StoredRecord>> _deleted = new();

        public IReadOnlyDictionary<string, Dictionary<string, StoredRecord>> Upserted => _upserted;
        public IReadOnlyDictionary<string, Dictionary<string, StoredRecord>> Deleted => _deleted;

        public void MarkUpserted(StoredRecord record)
        {
            var tableId = record.TableId;
            var key = record.Key;

            _deleted[tableId]?.Remove(key);

            if (!_upserted.TryGetValue(tableId, out var table))
                _upserted[tableId] = table = new();

            table[key] = record;
        }

        public void MarkDeleted(StoredRecord record)
        {
            var tableId = record.TableId;
            var key = record.Key;

            if (!_upserted.TryGetValue(tableId, out var table) || !table.Remove(key))
                return;

            if (!_deleted.TryGetValue(tableId, out var deletedTable))
                _deleted[tableId] = deletedTable = new();

            deletedTable[key] = record;
        }

        public void Clear()
        {
            _upserted.Clear();
            _deleted.Clear();
        }

        public InMemoryChangeSet CloneAndClear()
        {
            var clone = new InMemoryChangeSet();

            foreach (var kvp in _upserted)
            {
                clone._upserted[kvp.Key] = new Dictionary<string, StoredRecord>(kvp.Value);
            }

            foreach (var kvp in _deleted)
            {
                clone._deleted[kvp.Key] = new Dictionary<string, StoredRecord>(kvp.Value);
            }


            Clear();
            return clone;
        }

        public TableRecordChangeSet<TTableRecord> GetTableRecordChanges<TTableRecord>()
             where TTableRecord : ITableRecordSingleton, new()
        {
            var tableResource = ResourceRegistry.GetResource<TTableRecord>();
            var tableIdHex = tableResource.ResourceIdEncoded.ToHex(true);

            _upserted.TryGetValue(tableIdHex, out var upsertedRecords);
            _deleted.TryGetValue(tableIdHex, out var deletedRecords);

            return new TableRecordChangeSet<TTableRecord>
            {
                Upserted = GetTableRecords<TTableRecord>(upsertedRecords?.Values).ToList(),
                Deleted = GetTableRecords<TTableRecord>(deletedRecords?.Values).ToList()
            };
        }

        protected IEnumerable<TTableRecord> GetTableRecords<TTableRecord>(IEnumerable<StoredRecord> records) where TTableRecord : ITableRecordSingleton, new ()
        {
            var result = new List<TTableRecord>();
            if (records == null) return result;

            foreach (var record in records)
            {
                if (record == null) continue;

                var tableRecord = new TTableRecord();
                tableRecord.DecodeValues(record);

                if (tableRecord is ITableRecord tableRecordKey)
                {
                    tableRecordKey.DecodeKey(KeyUtils.ConvertKeyFromCombinedHex(record.Key));
                }

                result.Add(tableRecord);
            }

            return result;
        }
    }

}
