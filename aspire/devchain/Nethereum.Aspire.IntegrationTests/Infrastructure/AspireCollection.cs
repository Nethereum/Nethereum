using Xunit;

namespace Nethereum.Aspire.IntegrationTests.Infrastructure;

[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireFixture>
{
}
