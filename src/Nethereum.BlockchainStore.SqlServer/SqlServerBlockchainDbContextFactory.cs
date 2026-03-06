using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.SqlServer
{
    public class SqlServerBlockchainDbContextFactory : IBlockchainDbContextFactory
    {
        public static SqlServerBlockchainDbContextFactory Create(IConfigurationRoot config)
        {
            var connectionString = config.GetConnectionString("SqlServerConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = config.GetBlockchainStorageConnectionString();
            }
            return new SqlServerBlockchainDbContextFactory(connectionString);
        }

        private readonly string _connectionString;
        private readonly string _schema;

        public SqlServerBlockchainDbContextFactory(string connectionString, string schema = null)
        {
            _connectionString = connectionString;
            _schema = schema;
        }

        public BlockchainDbContextBase CreateContext()
        {
            return new SqlServerBlockchainDbContext(_connectionString, _schema);
        }
    }
}
