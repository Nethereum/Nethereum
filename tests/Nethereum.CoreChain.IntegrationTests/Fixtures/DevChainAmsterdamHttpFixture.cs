using Nethereum.DevChain.Configuration;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    public class DevChainAmsterdamHttpFixture : DevChainHttpFixture
    {
        public const int AmsterdamPort = 18547;
        public const string Fork = "amsterdam";

        public override int ServerPort => AmsterdamPort;

        protected override DevChainServerConfig CreateConfig() => new DevChainServerConfig
        {
            Port = ServerPort,
            ChainId = ChainId,
            Hardfork = Fork
        };
    }
}
