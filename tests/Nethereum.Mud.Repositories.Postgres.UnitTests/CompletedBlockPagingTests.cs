using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Repositories.EntityFramework;
using Nethereum.Mud.TableRepository;
using System.Numerics;

namespace Nethereum.Mud.Repositories.Postgres.UnitTests
{
    public class CompletedBlockPagingTests
    {
        private const string World = "0x1111111111111111111111111111111111111111";

        private static StoredRecord Record(byte key, long block) => new StoredRecord
        {
            AddressBytes = World.HexToByteArray(),
            TableIdBytes = new byte[32],
            KeyBytes = new byte[] { key },
            StaticData = new byte[] { key },
            EncodedLengths = new byte[0],
            DynamicData = new byte[0],
            BlockNumber = block
        };

        private static async Task<SqliteMudDatabase> Seed(long completedBlock)
        {
            var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            context.StoredRecords.AddRange(Record(1, 5), Record(2, 5), Record(3, 7));
            await context.SaveChangesAsync();
            await new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(context).UpsertProgressAsync(completedBlock);
            return database;
        }

        [Fact]
        public async Task PagingStopsAtTheLastCompletedBlock()
        {
            using var database = await Seed(5);
            using var context = database.CreateContext();
            var repository = new MudPostgresStoreRecordsTableRepository(context);

            var page = await repository.GetCompletedRecordsAsync(100, 0, 0);

            Assert.Equal(new byte[] { 1, 2 }, page.Records.Select(r => r.KeyBytes[0]).ToArray());
            Assert.DoesNotContain(page.Records, r => r.BlockNumber == 7);
        }

        [Fact]
        public async Task PagingResumesFromTheLastRowAndReturnsTheNextBlockOnceItIsCompleted()
        {
            using var database = await Seed(5);
            using var context = database.CreateContext();
            var repository = new MudPostgresStoreRecordsTableRepository(context);
            var first = await repository.GetCompletedRecordsAsync(1, 0, 0);

            var second = await repository.GetCompletedRecordsAsync(1, first.LastBlockNumber.Value, first.LastRowId.Value);
            var third = await repository.GetCompletedRecordsAsync(1, second.LastBlockNumber.Value, second.LastRowId.Value);
            await new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(context).UpsertProgressAsync(7);
            var fourth = await repository.GetCompletedRecordsAsync(1, second.LastBlockNumber.Value, second.LastRowId.Value);

            Assert.Equal((byte)1, first.Records.Single().KeyBytes[0]);
            Assert.Equal((byte)2, second.Records.Single().KeyBytes[0]);
            Assert.Empty(third.Records);
            Assert.Equal((byte)3, fourth.Records.Single().KeyBytes[0]);
        }

        [Fact]
        public async Task NothingIsReturnedBeforeAnyBlockIsCompleted()
        {
            using var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            context.StoredRecords.Add(Record(1, 5));
            await context.SaveChangesAsync();

            var page = await new MudPostgresStoreRecordsTableRepository(context).GetCompletedRecordsAsync(100, 0, 0);

            Assert.Empty(page.Records);
        }
    }
}
