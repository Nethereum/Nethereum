using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class InternalTransactionBlockProgressEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<InternalTransactionBlockProgress>
    {
        public void Configure(EntityTypeBuilder<InternalTransactionBlockProgress> entityBuilder)
        {
            entityBuilder.ToTable("InternalTransactionBlockProgress");
            entityBuilder.HasKey(b => b.RowIndex);

            entityBuilder.Property(b => b.LastBlockProcessed).IsRequired();
            entityBuilder.HasIndex(b => b.LastBlockProcessed);
        }
    }
}
