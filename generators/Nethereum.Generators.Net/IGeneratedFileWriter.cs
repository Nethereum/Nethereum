using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.Net
{
    public interface IGeneratedFileWriter
    {
        void WriteFile(GeneratedFile generatedFile);
        void WriteFiles(IEnumerable<GeneratedFile> generatedFiles);
    }
}