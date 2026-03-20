using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.SqlServer
{
    public class SqlServerBlockchainDbContext : BlockchainDbContextBase
    {
        private readonly string _connectionString;
        private readonly string _schema;

        public SqlServerBlockchainDbContext(string connectionString, string schema = null)
        {
            ColumnTypeForUnlimitedText = "nvarchar(max)";
            _connectionString = connectionString;
            _schema = schema;
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
#if NET10_0_OR_GREATER
                .ConfigureWarnings(w => w.Ignore(global::Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
#endif
                ;
        }
    }
}
