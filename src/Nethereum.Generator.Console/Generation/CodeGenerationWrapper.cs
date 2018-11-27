using Nethereum.Generator.Console.Configuration;
using Nethereum.Generators.Core;
using Nethereum.Generators.Net;

namespace Nethereum.Generator.Console.Generation
{
    public class CodeGenerationWrapper : ICodeGenerationWrapper
    {
        private readonly IGeneratorConfigurationFactory _codeGenConfigurationFactory;
        private readonly IGeneratedFileWriter _generatedFileWriter;

        public CodeGenerationWrapper()
        {
            _codeGenConfigurationFactory = new GeneratorConfigurationFactory();
            _generatedFileWriter = new GeneratedFileWriter();
        }

        public CodeGenerationWrapper(IGeneratorConfigurationFactory generatorConfigurationFactory, IGeneratedFileWriter generatedFileWriter)
        {
            _codeGenConfigurationFactory = generatorConfigurationFactory;
            _generatedFileWriter = generatedFileWriter;
        }

        public void FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder, bool singleFile)
        {
            var config = _codeGenConfigurationFactory.FromAbi(contractName, abiFilePath, binFilePath, baseNamespace, outputFolder);
            Generate(config, singleFile);
        }

        public void FromProject(string projectPath, string assemblyName)
        {
            var config = _codeGenConfigurationFactory.FromProject(projectPath, assemblyName);
            Generate(config);
        }

        public void FromDirectory(string inputDirectory, string baseNamespace, string outputFolder, bool singleFile)
        {
            var config = _codeGenConfigurationFactory.FromCompiledContractDirectory(inputDirectory, outputFolder, baseNamespace, CodeGenLanguage.CSharp);
            Generate(config, singleFile);
        }

        private void Generate(GeneratorConfiguration config, bool singleFile = true)
        {
            foreach (var generator in config.GetProjectGenerators())
            {
                System.Console.WriteLine($"Gen for {generator.ContractName}");
                var generatedFiles = singleFile ? generator.GenerateAllMessagesFileAndService() : generator.GenerateAll();
                _generatedFileWriter.WriteFiles(generatedFiles);
            }
        }
    }
}
