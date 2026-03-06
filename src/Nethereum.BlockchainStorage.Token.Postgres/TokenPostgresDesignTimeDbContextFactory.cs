using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nethereum.BlockchainStorage.Token.Postgres
{
    public class TokenPostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TokenPostgresDbContext>
    {
        public TokenPostgresDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TokenPostgresDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=design;Username=postgres;Password=postgres")
                .UseLowerCaseNamingConvention();
            return new TokenPostgresDbContext(optionsBuilder.Options);
        }
    }
}
