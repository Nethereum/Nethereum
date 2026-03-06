using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class ChainStateEntityBuilder : BaseEntityBuilder, IEntityTypeConfiguration<ChainState>
    {
        public void Configure(EntityTypeBuilder<ChainState> entityBuilder)
        {
            entityBuilder.ToTable("ChainStates");
            entityBuilder.HasKey(c => c.RowIndex);

            entityBuilder.HasIndex(c => c.LastCanonicalBlockNumber);

            entityBuilder.Property(c => c.LastCanonicalBlockHash).IsHash();
        }
    }
}
