using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nethereum.Mud.TableRepository;
using System.Numerics;
using Nethereum.Mud.Repositories.Postgres;

namespace Nethereum.Mud.Repositories.Postgres.UnitTests
{
    public sealed class SqliteMudDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly RowIdSequence _rowIds = new RowIdSequence();

        public SqliteMudDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public MudPostgresStoreRecordsDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<MudPostgresStoreRecordsDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_rowIds)
                .Options;
            return new SqliteMudStoreRecordsDbContext(options);
        }

        public void Dispose()
        {
            _connection.Dispose();
    }

        private sealed class SqliteMudStoreRecordsDbContext : MudPostgresStoreRecordsDbContext
        {
            public SqliteMudStoreRecordsDbContext(DbContextOptions<MudPostgresStoreRecordsDbContext> options) : base(options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<StoredRecord>()
                    .Property(r => r.BlockNumber)
                    .HasConversion(v => (long)v, v => (BigInteger)v)
                    .HasColumnType("INTEGER");
            }
        }

        private sealed class RowIdSequence : SaveChangesInterceptor
        {
            private int _last;

            public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            {
                Assign(eventData.Context);
                return result;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Assign(eventData.Context);
                return new ValueTask<InterceptionResult<int>>(result);
            }

            private void Assign(DbContext context)
            {
                foreach (var entry in context.ChangeTracker.Entries<StoredRecord>())
                {
                    if (entry.State == EntityState.Added && entry.Entity.RowId == 0)
                    {
                        entry.Entity.RowId = Interlocked.Increment(ref _last);
                    }
                }
            }
        }
    }
}
