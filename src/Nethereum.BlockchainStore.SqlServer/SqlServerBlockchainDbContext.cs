using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainStore.EFCore;
using Nethereum.Microsoft.Configuration.Utils;

namespace Nethereum.BlockchainStore.SqlServer
{
    public class SqlServerBlockchainDbContext : BlockchainDbContextBase
    {
        private readonly string _connectionString;
        private readonly string _schema;

        public SqlServerBlockchainDbContext() : this(GetConnectionString())
        {
        }

        public SqlServerBlockchainDbContext(string connectionString, string schema = null)
        {
            ColumnTypeForUnlimitedText = "nvarchar(max)";
            _connectionString = connectionString;
            _schema = schema;
        }

        private static string GetConnectionString()
        {
            var config = ConfigurationUtils.Build();
            var connectionString = config.GetConnectionString("SqlServerConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }

            return config.GetBlockchainStorageConnectionString();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (!string.IsNullOrWhiteSpace(_schema))
            {
                modelBuilder.HasDefaultSchema(_schema);
            }

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connectionString)
                .ConfigureWarnings(w => w.Ignore(global::Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }
    }
}
