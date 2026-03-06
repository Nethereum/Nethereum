using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nethereum.Mud.Repositories.Postgres
{
    public sealed class MudPostgresStoreRecordsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MudPostgresStoreRecordsDbContext>
    {
        public MudPostgresStoreRecordsDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("PostgresConnection")
                ?? Environment.GetEnvironmentVariable("MUD_POSTGRES_CONNECTION")
                ?? "Host=localhost;Database=MUDStore;Username=postgres;Password=password";

            var optionsBuilder = new DbContextOptionsBuilder<MudPostgresStoreRecordsDbContext>();
            optionsBuilder.UseNpgsql(
                connectionString,
                options => options.MigrationsAssembly(typeof(MudPostgresStoreRecordsDbContext).Assembly.FullName));

            return new MudPostgresStoreRecordsDbContext(optionsBuilder.Options);
        }
    }
}
