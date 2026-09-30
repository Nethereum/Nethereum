using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Storage;
using Newtonsoft.Json.Linq;

namespace Nethereum.MainnetChain.Hosting
{
    public static class MainnetGenesisLoader
    {
        public static async Task<int> PopulateAsync(IStateStore stateStore)
        {
            using var stream = OpenEmbeddedGenesis();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var genesis = JObject.Parse(json);
            var alloc = (JObject)genesis["alloc"]!;
            return await StandardGenesisLoader.PopulateAllocAsync(stateStore, alloc);
        }

        private static Stream OpenEmbeddedGenesis()
        {
            var asm = Assembly.GetExecutingAssembly();
            const string resourceName = "Nethereum.MainnetChain.Resources.MainnetGenesis.json";
            var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException(
                    $"Embedded resource '{resourceName}' not found. Available: " +
                    string.Join(", ", asm.GetManifestResourceNames()));
            return stream;
        }
    }
}
