using System;
using System.IO;

namespace Nethereum.Generators.UnitTests.Expectations
{
    public static class ExpectedContentRepository
    {
        public static string Get(string contentChildFolder, string resourceName)
        {
            var currentType = typeof(ExpectedContentRepository);
            var assembly = currentType.Assembly;
            var fullResourcePath = $"{currentType.Namespace}.Content.{contentChildFolder}.{resourceName}";

            using (var stream = assembly.GetManifestResourceStream(fullResourcePath))
            {
                if(stream == null)
                    throw new ArgumentException($"Could not find embedded resource with name '{fullResourcePath}' in assembly '{assembly.FullName}'.");

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
