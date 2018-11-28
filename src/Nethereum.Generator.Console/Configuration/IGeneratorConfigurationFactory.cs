using Nethereum.Generators.Core;

namespace Nethereum.Generator.Console.Configuration
{
    public interface IGeneratorConfigurationFactory
    {
        Models.Generator FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder);
        Models.Generator FromProject(string destinationProjectFolderOrFileName, string assemblyName);
        Models.Generator FromCompiledContractDirectory(string directory, string outputFolder, string baseNamespace,
            CodeGenLanguage language);
    }
}