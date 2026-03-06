using Microsoft.Extensions.Configuration;
using System;

namespace Nethereum.BlockchainStore.EFCore
{
    public interface IBlockchainDbContextFactory
    {
        BlockchainDbContextBase CreateContext();
    }

    public static class BlockchainDbContextConfigExtensions
    {
        public const string DbName = "BlockchainDbStorage";

        public static string GetBlockchainStorageDbSchema(this IConfigurationRoot config)
        {
            return config["DbSchema"];
        }

        public static string GetBlockchainStorageConnectionString(this IConfigurationRoot config, string schema = null)
        {
            var schemaPostFix = string.IsNullOrEmpty(schema) ? string.Empty : "_" + schema;
            var connectionStringName = $"{DbName}{schemaPostFix}";
            var connectionString = config.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(connectionString))
                throw new Exception($"Null or empty connection string connection string name: {connectionStringName}");

            return connectionString;
        }
    }
}