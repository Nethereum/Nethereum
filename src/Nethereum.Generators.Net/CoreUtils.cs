using Nethereum.Generators.Core;
using System.IO;
using Nethereum.Util;

namespace Nethereum.Generators.Net
{
    public static class CoreUtils
    {
        private static readonly Sha3Keccack Sha3 = new Sha3Keccack();

        public static string GetFullPath(this GeneratedFile generatedFile)
        {
            return Path.Combine(generatedFile.OutputFolder, generatedFile.FileName);
        }

        public static bool ExistsOnDiskWithSameContent(this GeneratedFile generatedFile)
        {
            if(!File.Exists(generatedFile.GetFullPath()))
                return false;

            var newHash = Sha3.CalculateHash(generatedFile.GeneratedCode);
            var existingHash = Sha3.CalculateHash(File.ReadAllText(generatedFile.GetFullPath()));
            return newHash == existingHash;
        }
    }
}
