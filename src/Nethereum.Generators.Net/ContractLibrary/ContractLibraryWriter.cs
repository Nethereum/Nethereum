

namespace Nethereum.Generators.Net.ContractLibrary
{
    public class ContractLibraryWriter
    {
        public void WriteClasses(GenerateClassesCommand command)
        {
            var contractAbi = new GeneratorModelABIDeserialiser().DeserialiseABI(command.Abi);
            var generator = new ContractProjectGenerator(
                contractAbi,
                command.ContractName, command.ContractByteCode, command.BaseNamespace,
                command.ServiceNamespace, command.CqsNamespace, command.DtoNamesapce, command.BasePath,
                command.PathDelimiter, command.CodeGenLanguage);
            var generatedClasses = generator.GenerateAll();
           GeneratedFileWriter.WriteFilesToDisk(generatedClasses);
        }

        public void WriteProjectFile(GenerateProjectFileCommand command)
        {
            var projectGenerator = new NetStandardLibraryGenerator(command.ProjectName, command.CodeLanguage);
            var generatedFile = projectGenerator.GenerateFileContent(command.Path);
            GeneratedFileWriter.WriteFileToDisk(generatedFile);
        }
    }
}
