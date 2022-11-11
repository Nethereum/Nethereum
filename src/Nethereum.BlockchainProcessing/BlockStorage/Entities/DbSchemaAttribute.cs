using System;

namespace Nethereum.BlockchainProcessing.BlockStorage.Entities
{
    public class DbSchemaAttribute: Attribute
    {
        public string DbSchemaName { get; }

        public DbSchemaAttribute(DbSchemaNames dbSchemaName)
        {
            DbSchemaName = dbSchemaName.ToString();
        }

        public DbSchemaAttribute(string dbSchemaName)
        {
            DbSchemaName = dbSchemaName;
        }
    }
}