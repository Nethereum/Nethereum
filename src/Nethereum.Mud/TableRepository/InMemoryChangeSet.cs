using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Mud.TableRepository
{
    public class InMemoryChangeSet
    {
        private readonly HashSet<StoredRecord> _created = new();
        private readonly HashSet<StoredRecord> _updated = new();
        private readonly HashSet<StoredRecord> _deleted = new();

        public IReadOnlyCollection<StoredRecord> Created => (IReadOnlyCollection<StoredRecord>)_created;
        public IReadOnlyCollection<StoredRecord> Updated => (IReadOnlyCollection<StoredRecord>)_updated;
        public IReadOnlyCollection<StoredRecord> Deleted => (IReadOnlyCollection<StoredRecord>)_deleted;

        public void MarkCreated(StoredRecord record)
        {
            _created.Add(record);
            _updated.Remove(record); // a created record cannot be also updated
            _deleted.Remove(record); // and shouldn't be considered deleted
        }

        public void MarkUpdated(StoredRecord record)
        {
            if (!_created.Contains(record)) // skip if it's newly created
                _updated.Add(record);
        }

        public void MarkDeleted(StoredRecord record)
        {
            _created.Remove(record); // if it was just created, forget it
            _updated.Remove(record);
            _deleted.Add(record);
        }

        public void Clear()
        {
            _created.Clear();
            _updated.Clear();
            _deleted.Clear();
        }

        public InMemoryChangeSet CloneAndClear()
        {
            var clone = new InMemoryChangeSet();
            foreach (var r in _created) clone._created.Add(r);
            foreach (var r in _updated) clone._updated.Add(r);
            foreach (var r in _deleted) clone._deleted.Add(r);
            Clear();
            return clone;
        }

        public TableRecordChangeSet<TTableRecord> GetTableRecordChanges<TTableRecord>()
         where TTableRecord : ITableRecordSingleton, new()
        {
            var tableResource = ResourceRegistry.GetResource<TTableRecord>();
            var tableId = tableResource.ResourceIdEncoded;

           
            return new TableRecordChangeSet<TTableRecord>
            {
                Created = GetTableRecords<TTableRecord>(_created
                    .Where(r => r.TableIdBytes.SequenceEqual(tableId)))
                    .ToList(),
                Updated = GetTableRecords<TTableRecord>(_updated
                    .Where(r => r.TableIdBytes.SequenceEqual(tableId)))
                    .ToList(),

                Deleted = GetTableRecords<TTableRecord>(_deleted
                    .Where(r => r.TableIdBytes.SequenceEqual(tableId)))
                    .ToList()
            };
        }

        protected IEnumerable<TTableRecord> GetTableRecords<TTableRecord>(IEnumerable<StoredRecord> records) where TTableRecord : ITableRecordSingleton, new ()
        {
            var result = new List<TTableRecord>();

            foreach (var record in records)
            {
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
