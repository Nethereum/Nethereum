using System.IO;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.Common
{
    public static class EmbeddedContentRepository
    {
        public static string Get(string resourceName)
        {
            var assembly = typeof(EmbeddedContentRepository).Assembly;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
