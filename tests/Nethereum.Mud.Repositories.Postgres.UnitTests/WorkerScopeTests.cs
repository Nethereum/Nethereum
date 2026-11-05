using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Mud.Repositories.Postgres.StoreRecordsNormaliser;
using Npgsql;

namespace Nethereum.Mud.Repositories.Postgres.UnitTests
{
    public class WorkerScopeTests
    {
        private const string World = "0x1111111111111111111111111111111111111111";

        private static ServiceProvider Build(SqliteMudDatabase database, List<MudPostgresStoreRecordsDbContext> created, Action<IServiceCollection> register)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ =>
            {
                var context = database.CreateContext();
                lock (created) created.Add(context);
                return context;
            });
            services.AddOptions<MudPostgresProcessingOptions>().Configure(o =>
            {
                o.Address = World;
                o.RpcUrl = "http://127.0.0.1:1";
            });
            register(services);
            return services.BuildServiceProvider();
        }

        private static async Task WaitForContexts(List<MudPostgresStoreRecordsDbContext> created, int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                lock (created) if (created.Count >= count) return;
                await Task.Delay(100);
            }
        }

        [Fact]
        public async Task ProcessingHostedService_UsesAFreshDbContextForEachRetryAttempt()
        {
            using var database = new SqliteMudDatabase();
            var created = new List<MudPostgresStoreRecordsDbContext>();
            using var provider = Build(database, created, s =>
            {
                s.AddTransient<MudPostgresStoreRecordsProcessingService>();
                s.AddSingleton<IHostedService>(sp => new MudPostgresProcessingHostedService(
                    sp.GetRequiredService<ILogger<MudPostgresProcessingHostedService>>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IOptions<MudPostgresProcessingOptions>>()));
            });

            var service = provider.GetRequiredService<IHostedService>();
            await service.StartAsync(CancellationToken.None);
            await WaitForContexts(created, 2);
            await service.StopAsync(CancellationToken.None);

            Assert.True(created.Count >= 2, $"expected a new DbContext per attempt but {created.Count} was created");
            Assert.NotSame(created[0], created[1]);
        }

        [Fact]
        public async Task NormaliserBackgroundService_UsesAFreshDbContextForEachRetryAttempt()
        {
            using var database = new SqliteMudDatabase();
            var created = new List<MudPostgresStoreRecordsDbContext>();
            using var provider = Build(database, created, s =>
            {
                s.AddTransient<MudPostgresStoreRecordsTableRepository>();
                s.AddTransient(_ => new NpgsqlConnection("Host=127.0.0.1;Port=1;Timeout=1;Username=u;Password=p;Database=d"));
                s.AddTransient<MudPostgresNormaliserProcessingService>();
                s.AddSingleton<IHostedService>(sp => new MudPostgresNormaliserBackgroundService(
                    sp.GetRequiredService<ILogger<MudPostgresNormaliserBackgroundService>>(),
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IOptions<MudPostgresProcessingOptions>>()));
            });

            var service = provider.GetRequiredService<IHostedService>();
            await service.StartAsync(CancellationToken.None);
            await WaitForContexts(created, 2);
            await service.StopAsync(CancellationToken.None);

            Assert.True(created.Count >= 2, $"expected a new DbContext per attempt but {created.Count} was created");
            Assert.NotSame(created[0], created[1]);
        }
    }
}
