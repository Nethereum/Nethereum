using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class ContractEntityBuilder:  BaseEntityBuilder, IEntityTypeConfiguration<Contract>
    {
        public void Configure(EntityTypeBuilder<Contract> entityBuilder)
        {
            entityBuilder.ToTable("Contracts");
            entityBuilder.HasKey(m => m.RowIndex);

            entityBuilder.Property(m => m.Address).IsAddress();
            entityBuilder.Property(m => m.Name).HasMaxLength(255);
            entityBuilder.Property(m => m.ABI).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.Code).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.Creator).IsAddress();
            entityBuilder.Property(m => m.TransactionHash).IsHash();

            entityBuilder.HasIndex(m => m.Name);
            entityBuilder.HasIndex(m => m.Address);
        }
    }
}
