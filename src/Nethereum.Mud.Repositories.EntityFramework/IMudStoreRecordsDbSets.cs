using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Mud.TableRepository;

namespace Nethereum.Mud.Repositories.EntityFramework
{

    public interface IMudStoreRecordsDbSets
    {
        public DbSet<StoredRecord> StoredRecords { get; set; }
        public DbSet<BlockProgress> BlockProgress { get; set; }
    }




}
