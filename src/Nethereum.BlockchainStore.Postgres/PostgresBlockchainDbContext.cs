using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.Postgres
{
    public class PostgresBlockchainDbContext : Nethereum.BlockchainStore.EFCore.BlockchainDbContextBase
    {
        private readonly string _connectionString;

        public PostgresBlockchainDbContext(string connectionString)
        {
            ColumnTypeForUnlimitedText = "text";
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString)
                .UseLowerCaseNamingConvention()
#if NET10_0_OR_GREATER
                .ConfigureWarnings(w => w.Ignore(global::Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
#endif
                ;
        }
    }
}
