using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Mud.TableRepository
{
    public class StoredRecordsDTOService
    {
        protected ITablePredicateQueryRepository TablePredicateQueryRepository { get; set; }

        public StoredRecordsDTOService(ITablePredicateQueryRepository tablePredicateQueryRepository)
        {
            TablePredicateQueryRepository = tablePredicateQueryRepository;
        }

        public async Task<IEnumerable<StoredRecordDTO>> GetStoredRecords(TablePredicate tablePredicate)
        {
            var results = await TablePredicateQueryRepository.GetRecordsAsync(tablePredicate);
            return results.MapToStoredRecordDTOs();
        }
    }

}
