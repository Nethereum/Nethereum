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

        public static bool ExistsOnDiskWithSameContent(this GeneratedFile generatedFile)
        {
            if(!File.Exists(generatedFile.GetFullPath()))
                return false;
            var existingFileText = File.ReadAllText(generatedFile.GetFullPath());
            return generatedFile.GeneratedCode.Equals(existingFileText);
        }
    }
}
