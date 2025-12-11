using Xunit;

namespace Nethereum.Consensus.LightClient.Tests.Live
{
    [CollectionDefinition("LiveTests", DisableParallelization = true)]
    public class LiveTestCollection : ICollectionFixture<LiveTestFixture>
    {
    }

    public class LiveTestFixture
    {
    }
}
