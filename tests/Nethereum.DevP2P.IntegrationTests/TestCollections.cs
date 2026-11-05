using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class HeavySecurityCollection
    {
        public const string Name = "DevP2PHeavySecurity";
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ExternalToolConformanceCollection
    {
        public const string Name = "DevP2PExternalToolConformance";
    }
}
