using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainStore.EFCore;

namespace Nethereum.BlockchainStore.Sqlite
{
    public class SqliteBlockchainDbContextFactory : IBlockchainDbContextFactory
    {
        public static SqliteBlockchainDbContextFactory Create(IConfigurationRoot config)
        {
            var connectionString = config.GetConnectionString("SqliteConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = config.GetBlockchainStorageConnectionString();
            }
            return new SqliteBlockchainDbContextFactory(connectionString);
        }

        private readonly string _connectionString;
        private readonly object _lock = new object();
        private bool _dbCreated;

        public SqliteBlockchainDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public BlockchainDbContextBase CreateContext()
        {
            var context = new SqliteBlockchainDbContext(_connectionString);
            if (!_dbCreated)
            {
                lock (_lock)
                {
                    if (!_dbCreated)
                    {
                        context.Database.EnsureCreated();
                        _dbCreated = true;
                    }
                }
            }
            return context;
        }
    }
}
