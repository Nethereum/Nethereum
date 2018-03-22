using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Generators.Core
{
    public class GeneratedFile
    {
        public string GeneratedCode { get; }
        public string FileName { get; }
        public string OutputFolder { get; }

        public GeneratedFile(string generatedCode, string fileName, string outputFolder)
        {
            GeneratedCode = generatedCode;
            FileName = fileName;
            OutputFolder = outputFolder;
        }
    }
}
