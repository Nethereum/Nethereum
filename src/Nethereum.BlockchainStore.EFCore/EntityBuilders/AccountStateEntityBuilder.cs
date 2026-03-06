using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class AccountStateEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<AccountState>
    {
        public void Configure(EntityTypeBuilder<AccountState> entityBuilder)
        {
            entityBuilder.ToTable("AccountStates");
            entityBuilder.HasKey(a => a.RowIndex);

            entityBuilder.HasIndex(a => a.Address).IsUnique();
            entityBuilder.HasIndex(a => a.LastUpdatedBlock);

            entityBuilder.Property(a => a.Address).IsAddress().IsRequired();
            entityBuilder.Property(a => a.Balance).IsBigInteger();
        }
    }
}
