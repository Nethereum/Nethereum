using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class TransactionLogVmStackEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<TransactionVmStack>
    {
        public void Configure(EntityTypeBuilder<TransactionVmStack> entityBuilder)
        {
            entityBuilder.ToTable("TransactionLogVmStacks");
            entityBuilder.HasKey(m => m.RowIndex);

            entityBuilder.Property(m => m.Address).IsAddress();
            entityBuilder.Property(m => m.TransactionHash).IsHash();
            entityBuilder.Property(m => m.StructLogs).IsUnlimitedText(ColumnTypeForUnlimitedText);

            entityBuilder.HasIndex(m => m.Address);
            entityBuilder.HasIndex(m => m.TransactionHash);
        }
    }
}
