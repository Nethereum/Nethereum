namespace Nethereum.Generator.Console.Generation
{
    public interface ICodeGenerationWrapper
    {
        void FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder);
        void FromProject(string projectPath, string assemblyName);
    }
}