using System.Threading.Tasks;

namespace Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser
{
    public interface IMudNormaliserProgressService
    {
        Task CreateProgressTableIfNotExistsAsync();
        Task UpsertProgressAsync(NormaliserProgressInfo progressInfo);
        Task<NormaliserProgressInfo> GetProgressAsync();
        Task ClearProgressAsync();
    }
}


