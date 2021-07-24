using Nethereum.Generators.Core;

namespace Nethereum.Generators.Net.ContractLibrary
{
    public class GenerateProjectFileCommand
    {
        public CodeGenLanguage CodeLanguage { get; set; }
        public string ProjectName { get; set; }
        public string Path { get; set; }
    }
}