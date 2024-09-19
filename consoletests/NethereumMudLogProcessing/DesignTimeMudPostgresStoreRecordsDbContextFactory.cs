using Nethereum.Mud.Repositories.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NethereumMudLogProcessing
{
    public class DesignTimeMudPostgresStoreRecordsDbContextFactory : IDesignTimeDbContextFactory<MudPostgresStoreRecordsDbContext>
    {
        public MudPostgresStoreRecordsDbContext CreateDbContext(string[] args)
        {
            // Load the configuration from the appsettings.json or other sources
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            // Get the connection string from the configuration
            var connectionString = configuration.GetConnectionString("PostgresConnection");

            // Configure the DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<MudPostgresStoreRecordsDbContext>();
            optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly("NethereumMudLogProcessing"))
                    .UseLowerCaseNamingConvention();

            return new MudPostgresStoreRecordsDbContext(optionsBuilder.Options
                 );
        }
    }


}

