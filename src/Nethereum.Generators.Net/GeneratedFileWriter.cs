using System.Collections.Generic;
using System.IO;
using Nethereum.Generators.Core;

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
            if (!string.IsNullOrEmpty(generatedFile.GeneratedCode))
            {
                if (!Directory.Exists(generatedFile.OutputFolder))
                    Directory.CreateDirectory(generatedFile.OutputFolder);

                using (var file = File.CreateText(Path.Combine(generatedFile.OutputFolder, generatedFile.FileName)))
                {
                    file.Write(generatedFile.GeneratedCode);
                    file.Flush();
                }
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
