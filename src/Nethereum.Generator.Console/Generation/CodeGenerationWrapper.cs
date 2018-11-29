using System.Collections.Generic;
using Nethereum.Generator.Console.Configuration;
using Nethereum.Generators;
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
            var projectGenerators = _codeGenConfigurationFactory.FromAbi(contractName, abiFilePath, binFilePath, baseNamespace, outputFolder);
            Generate(projectGenerators, singleFile);
        }

        public void FromProject(string projectPath, string assemblyName)
        {
            var projectGenerators = _codeGenConfigurationFactory.FromProject(projectPath, assemblyName);
            Generate(projectGenerators);
        }

        public void FromTruffle(string inputDirectory, string baseNamespace, string outputFolder, bool singleFile)
        {
            var projectGenerators = _codeGenConfigurationFactory.FromTruffle(inputDirectory, outputFolder, baseNamespace, CodeGenLanguage.CSharp);
            Generate(projectGenerators, singleFile);
        }

        private void Generate(IEnumerable<ContractProjectGenerator> projectGenerators, bool singleFile = true)
        {
            foreach (var generator in projectGenerators)
            {
                var generatedFiles = singleFile ? generator.GenerateAllMessagesFileAndService() : generator.GenerateAll();
                _generatedFileWriter.WriteFiles(generatedFiles);
            }
        }
    }
}
