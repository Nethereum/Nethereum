using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Mud.TableRepository
{


    public class InMemoryChangeTrackerTableRepository : InMemoryTableRepository
    {
        public InMemoryChangeSet ChangeSet { get; } = new();

        public override Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            TrackChange(tableId, key);
            return base.SetRecordAsync(tableId, key, encodedValues, address, blockNumber, logIndex);
        }

        public override Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            TrackChange(tableId, key);
            return base.SetRecordAsync(tableId, key, staticData, encodedLengths, dynamicData, address, blockNumber, logIndex);
        }

        public new Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();
            var tableIdHex = tableId.ToHex(true).EnsureHexPrefix();

            if (Tables.TryGetValue(tableIdHex, out var table) && table.TryGetValue(keyHex, out var record))
            {
                ChangeSet.MarkDeleted(record);
            }

            return base.DeleteRecordAsync(tableId, key, address, blockNumber, logIndex);
        }

        public new Task DeleteTableAsync(byte[] tableId)
        {
            var tableIdHex = tableId.ToHex(true).EnsureHexPrefix();

            if (Tables.TryGetValue(tableIdHex, out var records))
            {
                foreach (var record in records.Values)
                {
                    ChangeSet.MarkDeleted(record);
                }
            }

            return base.DeleteTableAsync(tableId);
        }

        private void TrackChange(byte[] tableId, List<byte[]> key)
        {
            var tableIdHex = tableId.ToHex(true).EnsureHexPrefix();
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            if (!Tables.TryGetValue(tableIdHex, out var table) || !table.TryGetValue(keyHex, out var record))
            {
                var newRecord = new StoredRecord { TableIdBytes = tableId, KeyBytes = ConvertKeyToCombinedHex(key).HexToByteArray() };
                ChangeSet.MarkCreated(newRecord);
            }
            else
            {
                ChangeSet.MarkUpdated(record);
            }
        }

        public InMemoryChangeSet GetAndClearChangeSet() => ChangeSet.CloneAndClear();

        public void ClearChangeSet()
        {
            ChangeSet.Clear();
        }
    }

}
