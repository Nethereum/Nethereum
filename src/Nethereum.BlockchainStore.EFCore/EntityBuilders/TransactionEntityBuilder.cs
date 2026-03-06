using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class TransactionEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> entityBuilder)
        {
            entityBuilder.ToTable("Transactions");
            entityBuilder.HasKey(b => b.RowIndex);

            entityBuilder.HasIndex(b => new {b.BlockNumber, b.Hash}).IsUnique();
            entityBuilder.HasIndex(b => b.Hash);
            entityBuilder.HasIndex(b => b.AddressFrom);
            entityBuilder.HasIndex(b => b.AddressTo);
            entityBuilder.HasIndex(b => b.NewContractAddress);
            entityBuilder.HasIndex(b => new { b.IsCanonical, b.BlockNumber });

            entityBuilder.Property(t => t.BlockHash).IsHash();
            entityBuilder.Property(t => t.BlockNumber).IsRequired();
            entityBuilder.Property(b => b.Hash).IsHash().IsRequired();
            entityBuilder.Property(b => b.AddressFrom).IsAddress();
            entityBuilder.Property(b => b.AddressTo).IsAddress();
            entityBuilder.Property(b => b.Value).IsBigInteger();
            entityBuilder.Property(b => b.Input).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(b => b.ReceiptHash).IsHash();
            entityBuilder.Property(b => b.Error).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(b => b.NewContractAddress).IsAddress();

            entityBuilder.Property(b => b.Gas).IsBigInteger();
            entityBuilder.Property(b => b.GasPrice).IsBigInteger();
            entityBuilder.Property(b => b.GasUsed).IsBigInteger();
            entityBuilder.Property(b => b.CumulativeGasUsed).IsBigInteger();

            entityBuilder.Property(b => b.EffectiveGasPrice).IsBigInteger();
            entityBuilder.Property(b => b.MaxFeePerGas).IsBigInteger();
            entityBuilder.Property(b => b.MaxPriorityFeePerGas).IsBigInteger();
            entityBuilder.Property(b => b.RevertReason).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(b => b.MaxFeePerBlobGas).IsBigInteger();
            entityBuilder.Property(b => b.BlobGasUsed).IsBigInteger();
            entityBuilder.Property(b => b.BlobGasPrice).IsBigInteger();
        }
    }
}
