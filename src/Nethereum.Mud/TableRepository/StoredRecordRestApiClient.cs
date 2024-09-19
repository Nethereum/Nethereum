using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Util;
using Nethereum.Util.Rest;


namespace Nethereum.Mud.TableRepository
{


    public class StoredRecordRestApiClient : ITablePredicateQueryRepository
    {
        private readonly IRestHttpHelper _httpHelper;
        private readonly string _baseUrl;
        private readonly string postPath;

        public StoredRecordRestApiClient(IRestHttpHelper httpHelper, string baseUrl, string postPath = "storedrecords")
        {
            _httpHelper = httpHelper;
            _baseUrl = baseUrl;
            this.postPath = postPath;
        }
        public async Task<List<StoredRecord>> GetRecordsAsync(TablePredicate tablePredicate)
        {
            var path = $"{_baseUrl}/{postPath}";
            var recordDTOs = await _httpHelper.PostAsync<IEnumerable<StoredRecordDTO>, TablePredicate>(path, tablePredicate);
            return recordDTOs.MapToStoredRecords().ToList();
        }

        public async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate) where TTableRecord : ITableRecord, new()
        {
            var records = await GetRecordsAsync(predicate);
            var result = new List<TTableRecord>();

            foreach (var record in records)
            {
                var tableRecord = new TTableRecord();
                tableRecord.DecodeValues(record);

                if (tableRecord is ITableRecord tableRecordKey)
                {
                    tableRecordKey.DecodeKey(record.KeyBytes.SplitBytes());
                }

                result.Add(tableRecord);
            }

            return result;
        }
    }

}
