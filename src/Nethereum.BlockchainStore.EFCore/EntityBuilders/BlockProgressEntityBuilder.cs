using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class BlockProgressEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<BlockProgress>
    {
        public void Configure(EntityTypeBuilder<BlockProgress> entityBuilder)
        {
            entityBuilder.ToTable("BlockProgress");
            entityBuilder.HasKey(b => b.RowIndex);

            entityBuilder.Property(b => b.LastBlockProcessed).IsRequired();
            entityBuilder.HasIndex(b => b.LastBlockProcessed);
        }
    }
}