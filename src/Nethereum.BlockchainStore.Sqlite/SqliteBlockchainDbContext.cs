using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.Sqlite
{
    public class SqliteBlockchainDbContext : BlockchainDbContextBase
    {
        private readonly string _connectionString;

        public SqliteBlockchainDbContext(string connectionString)
        {
            ColumnTypeForUnlimitedText = "TEXT";
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString)
#if NET10_0_OR_GREATER
                .ConfigureWarnings(w => w.Ignore(global::Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
#endif
                ;
        }
    }
}
