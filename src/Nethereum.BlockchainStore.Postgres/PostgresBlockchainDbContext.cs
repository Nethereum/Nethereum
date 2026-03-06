using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.Microsoft.Configuration.Utils;

namespace Nethereum.BlockchainStore.Postgres
{
    public class PostgresBlockchainDbContext : Nethereum.BlockchainStore.EFCore.BlockchainDbContextBase
    {
        private readonly string _connectionString;

        public PostgresBlockchainDbContext() : this(GetConnectionString())
        {
        }

        public PostgresBlockchainDbContext(string connectionString)
        {
            ColumnTypeForUnlimitedText = "text";
            _connectionString = connectionString;
        }

        private static string GetConnectionString()
        {
            var config = ConfigurationUtils.Build();
            var connectionString = config.GetConnectionString("PostgresConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            return config.GetBlockchainStorageConnectionString();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString)
                .UseLowerCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(global::Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    }
}
