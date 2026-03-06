using Microsoft.EntityFrameworkCore;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class MessageIndexDbContext : DbContext
    {
        public MessageIndexDbContext(DbContextOptions<MessageIndexDbContext> options) : base(options)
        {
        }

        public DbSet<IndexedMessage> IndexedMessages { get; set; } = null!;
        public DbSet<MessageBlockProgress> MessageBlockProgress { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexedMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.SourceChainId, e.MessageId }).IsUnique();
                entity.HasIndex(e => new { e.SourceChainId, e.BlockNumber });
                entity.Property(e => e.Data).HasColumnType("bytea");
            });

            modelBuilder.Entity<MessageBlockProgress>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SourceChainId).IsUnique();
                entity.Property(e => e.LastBlockProcessed).HasMaxLength(100);
            });
        }
    }
}
