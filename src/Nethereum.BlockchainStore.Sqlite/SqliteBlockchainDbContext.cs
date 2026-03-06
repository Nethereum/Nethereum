using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.Microsoft.Configuration.Utils;

namespace Nethereum.BlockchainStore.Sqlite
{
    public class SqliteBlockchainDbContext : BlockchainDbContextBase
    {
        private readonly string _connectionString;

        public SqliteBlockchainDbContext() : this(GetConnectionString())
        {
        }

        public SqliteBlockchainDbContext(string connectionString)
        {
            ColumnTypeForUnlimitedText = "TEXT";
            _connectionString = connectionString;
        }

        private static string GetConnectionString()
        {
            var config = ConfigurationUtils.Build();
            var connectionString = config.GetConnectionString("SqliteConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            return config.GetBlockchainStorageConnectionString();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString)
                .ConfigureWarnings(w => w.Ignore(global::Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    }
}
