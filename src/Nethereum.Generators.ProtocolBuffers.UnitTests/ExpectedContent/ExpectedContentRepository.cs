using Nethereum.Generators.ProtocolBuffers.UnitTests.Common;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.ExpectedContent
{
    public static class ExpectedContentRepository
    {
        public static string Get(string resourceName)
        {
            return EmbeddedContentRepository.Get(resourceName);
        }
    }
}
