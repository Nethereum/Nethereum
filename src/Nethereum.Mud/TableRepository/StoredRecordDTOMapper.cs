using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Nethereum.Mud.TableRepository
{
    public static class StoredRecordDTOMapper
    {
        public static StoredRecordDTO MapToStoredRecordDTO(this StoredRecord storedRecord)
        {
            return new StoredRecordDTO
            {
                TableId = storedRecord.TableId,
                Key = storedRecord.Key,
                Key0 = storedRecord.Key0,
                Key1 = storedRecord.Key1,
                Key2 = storedRecord.Key2,
                Key3 = storedRecord.Key3,
                Address = storedRecord.Address,
                BlockNumber = storedRecord.BlockNumber.ToString(),
                LogIndex = storedRecord.LogIndex,
                IsDeleted = storedRecord.IsDeleted,
                StaticData = storedRecord.StaticDataHex,
                DynamicData = storedRecord.DynamicDataHex,
                EncodedLengths = storedRecord.EncodedLengthsHex
            };
        }

        public static StoredRecord MapToStoredRecord(this StoredRecordDTO storedRecordDTO)
        {
            var storedRecord = new StoredRecord
            {
                TableId = storedRecordDTO.TableId,
                Key = storedRecordDTO.Key,
                Key0 = storedRecordDTO.Key0,
                Key1 = storedRecordDTO.Key1,
                Key2 = storedRecordDTO.Key2,
                Key3 = storedRecordDTO.Key3,
                Address = storedRecordDTO.Address,  
                LogIndex = storedRecordDTO.LogIndex,
                IsDeleted = storedRecordDTO.IsDeleted,
                StaticDataHex = storedRecordDTO.StaticData,
                DynamicDataHex = storedRecordDTO.DynamicData,
                EncodedLengthsHex = storedRecordDTO.EncodedLengths
            };

            if (BigInteger.TryParse(storedRecordDTO.BlockNumber, out var blockNumber))
            {
                storedRecord.BlockNumber = blockNumber;
            }

            return storedRecord;
        }

        public static IEnumerable<StoredRecordDTO> MapToStoredRecordDTOs(this IEnumerable<StoredRecord> storedRecords)
        {
            return storedRecords.Select(MapToStoredRecordDTO);
        }

        public static IEnumerable<StoredRecord> MapToStoredRecords(this IEnumerable<StoredRecordDTO> storedRecordDTOs)
        {
            return storedRecordDTOs.Select(MapToStoredRecord);
        }
    }
}
