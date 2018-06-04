using Nethereum.Generators.Core;
using System.IO;

namespace Nethereum.Generators.Net
{
    public static class CoreUtils
    {
        public static string GetFullPath(this GeneratedFile generatedFile)
        {
            return Path.Combine(generatedFile.OutputFolder, generatedFile.FileName);
        }
    }
}
