using Nethereum.Generators.Core;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.Generators.Net
{
    public class GeneratedFileWriter : IGeneratedFileWriter
    {
        public void WriteFiles(IEnumerable<GeneratedFile> generatedFiles)
        {
            WriteFilesToDisk(generatedFiles);
        }

        public void WriteFile(GeneratedFile generatedFile)
        {
            WriteFileToDisk(generatedFile);
        }

        public static void WriteFileToDisk(GeneratedFile generatedFile)
        {
            //soft handling empty code
            if (string.IsNullOrWhiteSpace(generatedFile.GeneratedCode))
                return;

            if (!Directory.Exists(generatedFile.OutputFolder))
                Directory.CreateDirectory(generatedFile.OutputFolder);

            if (generatedFile.ExistsOnDiskWithSameContent())
                return;

            using (var file = File.CreateText(generatedFile.GetFullPath()))
            {
                file.Write(generatedFile.GeneratedCode);
                file.Flush();
            }
        }

        public static void WriteFilesToDisk(IEnumerable<GeneratedFile> generatedFiles)
        {
            foreach (var generatedFile in generatedFiles)
            {
                WriteFileToDisk(generatedFile);
            }
        }
    }
}
