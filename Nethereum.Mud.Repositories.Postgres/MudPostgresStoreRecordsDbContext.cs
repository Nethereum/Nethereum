using Microsoft.EntityFrameworkCore;
using Nethereum.Mud.TableRepository;
using System.Numerics;
using Microsoft.Extensions.Configuration;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Mud.Repositories.EntityFramework;

namespace Nethereum.Mud.Repositories.Postgres
{
    public class MudPostgresStoreRecordsDbContext: DbContext, IMudStoreRecordsDbSets
    {
        public DbSet<StoredRecord> StoredRecords { get; set; }
        public DbSet<BlockProgress> BlockProgress { get; set; }
        public MudPostgresStoreRecordsDbContext()
        { }

        public MudPostgresStoreRecordsDbContext(DbContextOptions<MudPostgresStoreRecordsDbContext> options)
           : base(options) { 
        
            
        
        }
       
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            { 
                optionsBuilder
                    .UseLowerCaseNamingConvention();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BlockProgress>().HasKey(r => r.RowIndex);

            // Set primary key based on Address, TableId, and Key
            modelBuilder.Entity<StoredRecord>()
                .HasKey(r => new { r.AddressBytes, r.TableIdBytes, r.KeyBytes });
            
            // Index for querying by Address, TableId, Key0
            modelBuilder.Entity<StoredRecord>()
                .HasIndex(r => new { r.AddressBytes, r.TableIdBytes, r.Key0Bytes })
                .HasDatabaseName("IX_Address_TableId_Key0");

            // Index for querying by Address, TableId, Key1
            modelBuilder.Entity<StoredRecord>()
                .HasIndex(r => new { r.AddressBytes, r.TableIdBytes, r.Key1Bytes })
                .HasDatabaseName("IX_Address_TableId_Key1");

            // Index for querying by Address, TableId, Key2
            modelBuilder.Entity<StoredRecord>()
                .HasIndex(r => new { r.AddressBytes, r.TableIdBytes, r.Key2Bytes })
                .HasDatabaseName("IX_Address_TableId_Key2");

            // Index for querying by Address, TableId, Key3
            modelBuilder.Entity<StoredRecord>()
                .HasIndex(r => new { r.AddressBytes, r.TableIdBytes, r.Key3Bytes })
                .HasDatabaseName("IX_Address_TableId_Key3");

            // Configure BigInteger as decimal/numeric in PostgreSQL for BlockNumber
            modelBuilder.Entity<StoredRecord>()
                .Property(r => r.BlockNumber)
                .HasConversion(
                    v => (decimal)v,      // Convert BigInteger to decimal when saving
                    v => (BigInteger)v    // Convert decimal to BigInteger when retrieving
                )
                .HasColumnType("numeric(1000, 0)");

            // Map byte[] properties to bytea fields in PostgreSQL
            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.TableIdBytes)
                .HasColumnName("tableid")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.AddressBytes)
                .HasColumnName("address")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.KeyBytes)
                .HasColumnName("key")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.Key0Bytes)
                .HasColumnName("key0")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.Key1Bytes)
                .HasColumnName("key1")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.Key2Bytes)
                .HasColumnName("key2")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.Key3Bytes)
                .HasColumnName("key3")
                .HasColumnType("bytea");

            // Inherited properties from EncodedValues
            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.StaticData)
                .HasColumnName("static_data")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.EncodedLengths)
                .HasColumnName("encoded_lengths")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>()
                .Property(e => e.DynamicData)
                .HasColumnName("dynamic_data")
                .HasColumnType("bytea");

            modelBuilder.Entity<StoredRecord>().Ignore(e => e.TableId);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.Key);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.Key0);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.Key1);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.Key2);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.Key3);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.Address);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.StaticDataHex);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.EncodedLengthsHex);
            modelBuilder.Entity<StoredRecord>().Ignore(e => e.DynamicDataHex);
        }
    
    }




}
