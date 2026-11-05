using Nethereum.Mud.Repositories.EntityFramework;

namespace Nethereum.Mud.Repositories.Postgres.UnitTests
{
    public class BlockProgressCancellationTests
    {
        [Fact]
        public async Task ReadingProgressHonoursTheCancellationToken()
        {
            using var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            using var cancellation = new CancellationTokenSource();
            var repository = new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(context, cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetLastBlockNumberProcessedAsync());
        }

        [Fact]
        public async Task WritingProgressHonoursTheCancellationToken()
        {
            using var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            using var cancellation = new CancellationTokenSource();
            var repository = new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(context, cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.UpsertProgressAsync(5));

            using var verify = database.CreateContext();
            Assert.Null(await new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(verify).GetLastBlockNumberProcessedAsync());
        }

        [Fact]
        public async Task WithoutATokenProgressIsStoredAndRead()
        {
            using var database = new SqliteMudDatabase();
            using var context = database.CreateContext();
            var repository = new BlockProgressRepository<MudPostgresStoreRecordsDbContext>(context);

            await repository.UpsertProgressAsync(5);

            Assert.Equal(5, (int)(await repository.GetLastBlockNumberProcessedAsync()).Value);
        }
    }
}
