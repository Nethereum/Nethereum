namespace Nethereum.Generator.Console.Configuration
{
    public interface IGeneratorConfigurationFactory
    {
        GeneratorConfiguration FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder);
        GeneratorConfiguration FromProject(string destinationProjectFolderOrFileName, string assemblyName);
    }
}