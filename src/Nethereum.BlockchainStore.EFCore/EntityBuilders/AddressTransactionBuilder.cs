using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class AddressTransactionBuilder: BaseEntityBuilder, IEntityTypeConfiguration<AddressTransaction>
    {
        public void Configure(EntityTypeBuilder<AddressTransaction> entityBuilder)
        {
            entityBuilder.ToTable("AddressTransactions");
            entityBuilder.HasKey(b => b.RowIndex);

            entityBuilder.HasIndex(b => new {b.BlockNumber, b.Hash, b.Address}).IsUnique();
            entityBuilder.HasIndex(b => b.Hash);
            entityBuilder.HasIndex(b => b.Address);
            entityBuilder.HasIndex(b => new { b.Address, b.BlockNumber });
            entityBuilder.Property(t => t.BlockNumber).IsRequired();
            entityBuilder.Property(b => b.Hash).IsHash().IsRequired();
            entityBuilder.Property(b => b.Address).IsAddress().IsRequired();
        }
    }
}
