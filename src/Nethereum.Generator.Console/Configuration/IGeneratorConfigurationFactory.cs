using Nethereum.Generators.Core;

namespace Nethereum.Generator.Console.Configuration
{
    public interface IGeneratorConfigurationFactory
    {
        GeneratorConfiguration FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder);
        GeneratorConfiguration FromProject(string destinationProjectFolderOrFileName, string assemblyName);
        GeneratorConfiguration FromCompiledContractDirectory(string directory, string outputFolder, string baseNamespace,
            CodeGenLanguage language);
    }
}