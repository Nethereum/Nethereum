using Microsoft.EntityFrameworkCore.Design;

namespace Nethereum.BlockchainStore.SqlServer
{
    public class SqlServerBlockchainDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqlServerBlockchainDbContext>
    {
        public SqlServerBlockchainDbContext CreateDbContext(string[] args)
        {
            return new SqlServerBlockchainDbContext("Server=(localdb)\\mssqllocaldb;Database=BlockchainStorage;Trusted_Connection=True;");
        }
    }
}
