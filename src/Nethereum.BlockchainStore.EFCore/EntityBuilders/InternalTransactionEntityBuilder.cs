using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class InternalTransactionEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<InternalTransaction>
    {
        public void Configure(EntityTypeBuilder<InternalTransaction> entityBuilder)
        {
            entityBuilder.ToTable("InternalTransactions");
            entityBuilder.HasKey(m => m.RowIndex);

            entityBuilder.Property(m => m.TransactionHash).IsHash().IsRequired();
            entityBuilder.Property(m => m.BlockHash).IsHash();
            entityBuilder.Property(m => m.AddressFrom).IsAddress();
            entityBuilder.Property(m => m.AddressTo).IsAddress();
            entityBuilder.Property(m => m.Value).IsBigInteger();
            entityBuilder.Property(m => m.Gas).IsBigInteger();
            entityBuilder.Property(m => m.GasUsed).IsBigInteger();
            entityBuilder.Property(m => m.Type).HasMaxLength(20);
            entityBuilder.Property(m => m.Input).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.Output).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.Error).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(m => m.RevertReason).IsUnlimitedText(ColumnTypeForUnlimitedText);

            entityBuilder.HasIndex(m => new { m.TransactionHash, m.TraceIndex }).IsUnique();
            entityBuilder.HasIndex(m => m.AddressFrom);
            entityBuilder.HasIndex(m => m.AddressTo);
            entityBuilder.HasIndex(m => new { m.IsCanonical, m.BlockNumber });
            entityBuilder.HasIndex(m => m.BlockNumber);
        }
    }
}
