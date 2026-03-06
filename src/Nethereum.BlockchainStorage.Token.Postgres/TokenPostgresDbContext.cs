using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainStorage.Token.Postgres.Repositories;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public class TokenPostgresDbContext : DbContext
    {
        public DbSet<TokenTransferLog> TokenTransferLogs { get; set; }
        public DbSet<TokenBalance> TokenBalances { get; set; }
        public DbSet<NFTInventory> NFTInventory { get; set; }
        public DbSet<TokenMetadata> TokenMetadata { get; set; }
        public DbSet<BlockProgress> BlockProgress { get; set; }
        public DbSet<BalanceAggregationProgress> BalanceAggregationProgress { get; set; }
        public DbSet<DenormalizerProgress> DenormalizerProgress { get; set; }
        public DbSet<TransactionLog> IndexedLogs { get; set; }

        public TokenPostgresDbContext()
        { }

        public TokenPostgresDbContext(DbContextOptions<TokenPostgresDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseLowerCaseNamingConvention();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BlockProgress>().ToTable("TokenBlockProgress");
            modelBuilder.Entity<BlockProgress>().HasKey(r => r.RowIndex);
            modelBuilder.Entity<BlockProgress>()
                .HasIndex(b => b.LastBlockProcessed)
                .HasDatabaseName("IX_TokenBlockProgress_LastBlockProcessed");

            modelBuilder.Entity<TokenTransferLog>(entity =>
            {
                entity.HasKey(e => e.RowIndex);
                entity.Property(e => e.RowIndex).ValueGeneratedOnAdd();

                entity.HasIndex(e => new { e.TransactionHash, e.LogIndex })
                    .HasDatabaseName("IX_TokenTransferLogs_TxHash_LogIndex")
                    .IsUnique();

                entity.HasIndex(e => e.BlockNumber)
                    .HasDatabaseName("IX_TokenTransferLogs_BlockNumber");

                entity.HasIndex(e => e.FromAddress)
                    .HasDatabaseName("IX_TokenTransferLogs_FromAddress");

                entity.HasIndex(e => e.ToAddress)
                    .HasDatabaseName("IX_TokenTransferLogs_ToAddress");

                entity.HasIndex(e => e.ContractAddress)
                    .HasDatabaseName("IX_TokenTransferLogs_ContractAddress");

                entity.HasIndex(e => e.IsCanonical)
                    .HasDatabaseName("IX_TokenTransferLogs_IsCanonical");

                entity.Property(e => e.TransactionHash).HasMaxLength(67);
                entity.Property(e => e.BlockHash).HasMaxLength(67);
                entity.Property(e => e.ContractAddress).HasMaxLength(43);
                entity.Property(e => e.EventHash).HasMaxLength(67);
                entity.Property(e => e.FromAddress).HasMaxLength(43);
                entity.Property(e => e.ToAddress).HasMaxLength(43);
                entity.Property(e => e.Amount).HasMaxLength(100);
                entity.Property(e => e.TokenId).HasMaxLength(100);
                entity.Property(e => e.OperatorAddress).HasMaxLength(43);
                entity.Property(e => e.TokenType).HasMaxLength(10);
            });

            modelBuilder.Entity<TokenBalance>(entity =>
            {
                entity.HasKey(e => e.RowIndex);
                entity.Property(e => e.RowIndex).ValueGeneratedOnAdd();

                entity.HasIndex(e => new { e.Address, e.ContractAddress })
                    .HasDatabaseName("IX_TokenBalances_Address_Contract")
                    .IsUnique();

                entity.HasIndex(e => e.ContractAddress)
                    .HasDatabaseName("IX_TokenBalances_ContractAddress");

                entity.Property(e => e.Address).HasMaxLength(43);
                entity.Property(e => e.ContractAddress).HasMaxLength(43);
                entity.Property(e => e.Balance).HasMaxLength(100);
                entity.Property(e => e.TokenType).HasMaxLength(10);
            });

            modelBuilder.Entity<NFTInventory>(entity =>
            {
                entity.HasKey(e => e.RowIndex);
                entity.Property(e => e.RowIndex).ValueGeneratedOnAdd();

                entity.HasIndex(e => new { e.Address, e.ContractAddress, e.TokenId })
                    .HasDatabaseName("IX_NFTInventory_Address_Contract_TokenId")
                    .IsUnique();

                entity.HasIndex(e => e.Address)
                    .HasDatabaseName("IX_NFTInventory_Address");

                entity.HasIndex(e => new { e.ContractAddress, e.TokenId })
                    .HasDatabaseName("IX_NFTInventory_Contract_TokenId");

                entity.Property(e => e.Address).HasMaxLength(43);
                entity.Property(e => e.ContractAddress).HasMaxLength(43);
                entity.Property(e => e.TokenId).HasMaxLength(100);
                entity.Property(e => e.Amount).HasMaxLength(100);
                entity.Property(e => e.TokenType).HasMaxLength(10);
            });

            modelBuilder.Entity<TokenMetadata>(entity =>
            {
                entity.HasKey(e => e.RowIndex);
                entity.Property(e => e.RowIndex).ValueGeneratedOnAdd();

                entity.HasIndex(e => e.ContractAddress)
                    .HasDatabaseName("IX_TokenMetadata_ContractAddress")
                    .IsUnique();

                entity.Property(e => e.ContractAddress).HasMaxLength(43);
                entity.Property(e => e.Name).HasMaxLength(256);
                entity.Property(e => e.Symbol).HasMaxLength(32);
                entity.Property(e => e.TokenType).HasMaxLength(10);
            });

            modelBuilder.Entity<BalanceAggregationProgress>(entity =>
            {
                entity.HasKey(e => e.RowIndex);
                entity.HasIndex(b => b.LastProcessedRowIndex)
                    .HasDatabaseName("IX_BalanceAggregationProgress_LastProcessedRowIndex");
            });

            modelBuilder.Entity<DenormalizerProgress>(entity =>
            {
                entity.HasKey(e => e.RowIndex);
                entity.HasIndex(e => e.LastProcessedRowIndex)
                    .HasDatabaseName("IX_DenormalizerProgress_LastProcessedRowIndex");
            });

            modelBuilder.Entity<TransactionLog>(entity =>
            {
                entity.ToTable("TransactionLogs", t => t.ExcludeFromMigrations());
                entity.HasKey(e => e.RowIndex);
            });
        }
    }
}
