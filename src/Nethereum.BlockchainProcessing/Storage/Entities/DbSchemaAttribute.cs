using System;

namespace Nethereum.BlockchainProcessing.Storage.Entities
{
    public class DbSchemaAttribute: Attribute
    {
        public DbSchemaNames DbSchemaName { get; }

        public DbSchemaAttribute(DbSchemaNames dbSchemaName)
        {
            DbSchemaName = dbSchemaName;
        }
    }
}