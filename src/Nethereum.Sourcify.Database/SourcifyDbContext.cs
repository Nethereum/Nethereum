using Microsoft.EntityFrameworkCore;
using Nethereum.DataServices.Sourcify.Database.Models;

namespace Nethereum.Sourcify.Database
{
    public class SourcifyDbContext : DbContext
    {
        public SourcifyDbContext(DbContextOptions<SourcifyDbContext> options) : base(options)
        {
        }

        public DbSet<Code> Codes { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<ContractDeployment> ContractDeployments { get; set; }
        public DbSet<CompiledContract> CompiledContracts { get; set; }
        public DbSet<Source> Sources { get; set; }
        public DbSet<CompiledContractSource> CompiledContractSources { get; set; }
        public DbSet<Signature> Signatures { get; set; }
        public DbSet<CompiledContractSignature> CompiledContractSignatures { get; set; }
        public DbSet<VerifiedContract> VerifiedContracts { get; set; }
        public DbSet<SourcifyMatch> SourcifyMatches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Code>(entity =>
            {
                entity.ToTable("code");
                entity.HasKey(e => e.CodeHash);
                entity.Property(e => e.CodeHash).HasColumnName("code_hash");
                entity.Property(e => e.CodeHashKeccak).HasColumnName("code_hash_keccak");
                entity.Property(e => e.CodeBytes).HasColumnName("code");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("contracts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.CreationCodeHash).HasColumnName("creation_code_hash");
                entity.Property(e => e.RuntimeCodeHash).HasColumnName("runtime_code_hash");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<ContractDeployment>(entity =>
            {
                entity.ToTable("contract_deployments");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ChainId).HasColumnName("chain_id");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.TransactionHash).HasColumnName("transaction_hash");
                entity.Property(e => e.BlockNumber).HasColumnName("block_number");
                entity.Property(e => e.TransactionIndex).HasColumnName("transaction_index");
                entity.Property(e => e.Deployer).HasColumnName("deployer");
                entity.Property(e => e.ContractId).HasColumnName("contract_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

                entity.HasIndex(e => new { e.ChainId, e.Address }).IsUnique();
            });

            modelBuilder.Entity<CompiledContract>(entity =>
            {
                entity.ToTable("compiled_contracts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Compiler).HasColumnName("compiler");
                entity.Property(e => e.Version).HasColumnName("version");
                entity.Property(e => e.Language).HasColumnName("language");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.FullyQualifiedName).HasColumnName("fully_qualified_name");
                entity.Property(e => e.CompilerSettings).HasColumnName("compiler_settings").HasColumnType("jsonb");
                entity.Property(e => e.CompilationArtifacts).HasColumnName("compilation_artifacts").HasColumnType("jsonb");
                entity.Property(e => e.CreationCodeHash).HasColumnName("creation_code_hash");
                entity.Property(e => e.CreationCodeArtifacts).HasColumnName("creation_code_artifacts").HasColumnType("jsonb");
                entity.Property(e => e.RuntimeCodeHash).HasColumnName("runtime_code_hash");
                entity.Property(e => e.RuntimeCodeArtifacts).HasColumnName("runtime_code_artifacts").HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<Source>(entity =>
            {
                entity.ToTable("sources");
                entity.HasKey(e => e.SourceHash);
                entity.Property(e => e.SourceHash).HasColumnName("source_hash");
                entity.Property(e => e.SourceHashKeccak).HasColumnName("source_hash_keccak");
                entity.Property(e => e.Content).HasColumnName("content");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<CompiledContractSource>(entity =>
            {
                entity.ToTable("compiled_contracts_sources");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.CompilationId).HasColumnName("compilation_id");
                entity.Property(e => e.SourceHash).HasColumnName("source_hash");
                entity.Property(e => e.Path).HasColumnName("path");
            });

            modelBuilder.Entity<Signature>(entity =>
            {
                entity.ToTable("signatures");
                entity.HasKey(e => e.SignatureHash32);
                entity.Property(e => e.SignatureHash32).HasColumnName("signature_hash");
                entity.Property(e => e.SignatureHash4).HasColumnName("selector");
                entity.Property(e => e.SignatureText).HasColumnName("signature");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                entity.HasIndex(e => e.SignatureHash4);
            });

            modelBuilder.Entity<CompiledContractSignature>(entity =>
            {
                entity.ToTable("compiled_contracts_signatures");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.CompilationId).HasColumnName("compilation_id");
                entity.Property(e => e.SignatureHash32).HasColumnName("signature_hash");
                entity.Property(e => e.SignatureType).HasColumnName("type").HasConversion<string>();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<VerifiedContract>(entity =>
            {
                entity.ToTable("verified_contracts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.DeploymentId).HasColumnName("deployment_id");
                entity.Property(e => e.CompilationId).HasColumnName("compilation_id");
                entity.Property(e => e.CreationMatch).HasColumnName("creation_match");
                entity.Property(e => e.CreationValues).HasColumnName("creation_values").HasColumnType("jsonb");
                entity.Property(e => e.CreationTransformations).HasColumnName("creation_transformations").HasColumnType("jsonb");
                entity.Property(e => e.CreationMetadataMatch).HasColumnName("creation_metadata_match");
                entity.Property(e => e.RuntimeMatch).HasColumnName("runtime_match");
                entity.Property(e => e.RuntimeValues).HasColumnName("runtime_values").HasColumnType("jsonb");
                entity.Property(e => e.RuntimeTransformations).HasColumnName("runtime_transformations").HasColumnType("jsonb");
                entity.Property(e => e.RuntimeMetadataMatch).HasColumnName("runtime_metadata_match");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            modelBuilder.Entity<SourcifyMatch>(entity =>
            {
                entity.ToTable("sourcify_matches");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.VerifiedContractId).HasColumnName("verified_contract_id");
                entity.Property(e => e.CreationMatch).HasColumnName("creation_match");
                entity.Property(e => e.RuntimeMatch).HasColumnName("runtime_match");
                entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });
        }
    }
}
