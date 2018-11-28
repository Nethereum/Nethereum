using Nethereum.Generators.Core;

namespace Nethereum.Generator.Console.Configuration
{
    public interface IGeneratorConfigurationFactory
    {
        Models.ProjectGenerator FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder);
        Models.ProjectGenerator FromProject(string destinationProjectFolderOrFileName, string assemblyName);
        Models.ProjectGenerator FromTruffle(string directory, string outputFolder, string baseNamespace,
            CodeGenLanguage language);
    }
}