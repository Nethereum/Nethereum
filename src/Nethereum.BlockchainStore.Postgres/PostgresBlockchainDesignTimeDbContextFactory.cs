using Microsoft.EntityFrameworkCore.Design;

namespace Nethereum.BlockchainStore.Postgres
{
    public class PostgresBlockchainDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgresBlockchainDbContext>
    {
        public PostgresBlockchainDbContext CreateDbContext(string[] args)
        {
            return new PostgresBlockchainDbContext("Host=localhost;Database=design;Username=postgres;Password=postgres");
        }
    }
}
