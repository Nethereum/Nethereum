using Microsoft.EntityFrameworkCore.Design;

namespace Nethereum.BlockchainStore.Sqlite
{
    public class SqliteBlockchainDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteBlockchainDbContext>
    {
        public SqliteBlockchainDbContext CreateDbContext(string[] args)
        {
            return new SqliteBlockchainDbContext("Data Source=design.db");
        }
    }
}
