using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Generators.Core
{
    public class GeneratedClass
    {
        public string GeneratedCode { get; }
        public string FileName { get; }
        public string OutputFolder { get; }

        public GeneratedClass(string generatedCode, string fileName, string outputFolder)
        {
            GeneratedCode = generatedCode;
            FileName = fileName;
            OutputFolder = outputFolder;
        }
    }
}
