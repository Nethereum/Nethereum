using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;

namespace Nethereum.BlockchainStore.EFCore.EntityBuilders
{
    public class BlockEntityBuilder: BaseEntityBuilder, IEntityTypeConfiguration<Block>
    {
        public void Configure(EntityTypeBuilder<Block> entityBuilder)
        {
            entityBuilder.ToTable("Blocks");
            entityBuilder.HasKey(b => b.RowIndex);

            entityBuilder.Property(b => b.BlockNumber).IsRequired();
            entityBuilder.Property(b => b.Hash).IsHash().IsRequired();
            entityBuilder.Property(b => b.ParentHash).IsHash().IsRequired();
            entityBuilder.Property(b => b.Miner).IsAddress();

            entityBuilder.Property(b => b.Difficulty).IsBigInteger();
            entityBuilder.Property(b => b.TotalDifficulty).IsBigInteger();
            entityBuilder.Property(b => b.Size).IsBigInteger();
            entityBuilder.Property(b => b.GasLimit).IsBigInteger();
            entityBuilder.Property(b => b.GasUsed).IsBigInteger();
            entityBuilder.Property(b => b.Timestamp).IsRequired();
            entityBuilder.Property(b => b.Nonce).IsBigInteger();
            entityBuilder.Property(b => b.BaseFeePerGas).IsBigInteger();
            entityBuilder.Property(b => b.StateRoot).IsHash();
            entityBuilder.Property(b => b.ReceiptsRoot).IsHash();
            entityBuilder.Property(b => b.LogsBloom).IsUnlimitedText(ColumnTypeForUnlimitedText);
            entityBuilder.Property(b => b.WithdrawalsRoot).IsBigInteger();
            entityBuilder.Property(b => b.BlobGasUsed).IsBigInteger();
            entityBuilder.Property(b => b.ExcessBlobGas).IsBigInteger();
            entityBuilder.Property(b => b.ParentBeaconBlockRoot).IsHash();
            entityBuilder.Property(b => b.RequestsHash).IsHash();
            entityBuilder.Property(b => b.TransactionsRoot).IsHash();
            entityBuilder.Property(b => b.MixHash).IsHash();
            entityBuilder.Property(b => b.Sha3Uncles).IsHash();

            entityBuilder.HasIndex(b => new {b.BlockNumber, b.Hash}).IsUnique();
            entityBuilder.HasIndex(b => b.BlockNumber);
            entityBuilder.HasIndex(b => b.Hash);
            entityBuilder.HasIndex(b => b.ParentHash);
            entityBuilder.HasIndex(b => new { b.IsCanonical, b.BlockNumber });
        }
    }
}
