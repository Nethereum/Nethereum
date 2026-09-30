using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Postgres.Entities;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class AnchorIndexDbContext : DbContext
    {
        public AnchorIndexDbContext() { }

        public AnchorIndexDbContext(DbContextOptions<AnchorIndexDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseLowerCaseNamingConvention();
            }
        }

        public DbSet<AnchorRecord> Anchors { get; set; }
        public DbSet<ChainRegistration> ChainRegistrations { get; set; }
        public DbSet<BlockProofRecord> BlockProofs { get; set; }
        public DbSet<ChainAnchoringSummary> ChainSummaries { get; set; }
        public DbSet<AnchorIndexProgress> IndexProgress { get; set; }
        public DbSet<AnchorDenormalizerProgress> DenormalizerProgress { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AnchorRecord>(entity =>
            {
                entity.ToTable("anchor_records");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ChainId, e.EndBlock }).IsUnique();
                entity.HasIndex(e => e.ChainId);
                entity.HasIndex(e => e.TransactionHash);
                entity.Property(e => e.EndBlockHash).HasColumnType("bytea");
                entity.Property(e => e.PostStateRoot).HasColumnType("bytea");
                entity.Property(e => e.BlockHashesRoot).HasColumnType("bytea");
                entity.Property(e => e.ManifestHash).HasColumnType("bytea");
                entity.Property(e => e.PreviousAnchorHash).HasColumnType("bytea");
            });

            modelBuilder.Entity<ChainRegistration>(entity =>
            {
                entity.ToTable("anchor_chain_registrations");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ChainId).IsUnique();
                entity.Property(e => e.GenesisHash).HasColumnType("bytea");
            });

            modelBuilder.Entity<BlockProofRecord>(entity =>
            {
                entity.ToTable("anchor_block_proofs");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ChainId, e.BlockNumber }).IsUnique();
                entity.HasIndex(e => e.ChainId);
            });

            modelBuilder.Entity<ChainAnchoringSummary>(entity =>
            {
                entity.ToTable("anchor_chain_summaries");
                entity.HasKey(e => e.ChainId);
            });

            modelBuilder.Entity<AnchorIndexProgress>(entity =>
            {
                entity.ToTable("anchor_index_progress");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<AnchorDenormalizerProgress>(entity =>
            {
                entity.ToTable("anchor_denormalizer_progress");
                entity.HasKey(e => e.Id);
            });
        }
    }
}
