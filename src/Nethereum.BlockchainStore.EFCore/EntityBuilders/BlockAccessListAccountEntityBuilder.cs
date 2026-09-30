using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class BlockAccessListAccountEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<BlockAccessListAccount>
    {
        public void Configure(EntityTypeBuilder<BlockAccessListAccount> entityBuilder)
        {
            entityBuilder.ToTable("BlockAccessListAccounts");
            entityBuilder.HasKey(m => m.RowIndex);

            entityBuilder.Property(m => m.Address).IsAddress();
            entityBuilder.Property(m => m.BlockHash).IsHash();
            entityBuilder.Property(m => m.StorageReads).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.StorageChanges).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.BalanceChanges).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.NonceChanges).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.CodeChanges).IsUnlimitedText(ColumnTypeForUnlimitedText);

            entityBuilder.HasIndex(m => new { m.BlockNumber, m.Address }).IsUnique();
            entityBuilder.HasIndex(m => m.Address);
            entityBuilder.HasIndex(m => new { m.IsCanonical, m.BlockNumber });
        }
    }
}
