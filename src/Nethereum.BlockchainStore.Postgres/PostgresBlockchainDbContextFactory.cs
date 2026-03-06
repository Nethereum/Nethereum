using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.Postgres
{
    public class PostgresBlockchainDbContextFactory : IBlockchainDbContextFactory
    {
        public static PostgresBlockchainDbContextFactory Create(IConfigurationRoot config)
        {
            var connectionString = config.GetConnectionString("PostgresConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = config.GetBlockchainStorageConnectionString();
            }
            return new PostgresBlockchainDbContextFactory(connectionString);
        }

        private readonly string _connectionString;

        public PostgresBlockchainDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public BlockchainDbContextBase CreateContext()
        {
            return new PostgresBlockchainDbContext(_connectionString);
        }
    }
}
