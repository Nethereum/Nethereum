using System.IO;
using System.Reflection;
using Nethereum.Generators.Core;
using Nethereum.Generators.Net;
using Nethereum.Generators.Tests.Common;
using Nethereum.Generators.UnitTests.TestData;
using Xunit;

namespace Nethereum.Generators.IntegrationTests
{
    public class NetStandardLibraryGeneratorEndToEndTests
    {
        [Theory]
        [InlineData(CodeGenLanguage.Vb)]
        [InlineData(CodeGenLanguage.CSharp)]
        [InlineData(CodeGenLanguage.FSharp)]
        public void GeneratedProjectBuildsSuccessfully(CodeGenLanguage codeGenLanguage)
        {
            var context = new ProjectTestContext(GetType().Name,
                $"{MethodBase.GetCurrentMethod().Name}{codeGenLanguage}");

            try
            {
                //given
                context.TargetFramework = "netstandard2.0";
                context.CreateEmptyProject();
                var fullProjectFilePath = Path.Combine(context.TargetProjectFolder,
                    context.ProjectName + CodeGenLanguageExt.ProjectFileExtensions[codeGenLanguage]);

                var generator = new NetStandardLibraryGenerator(fullProjectFilePath, codeGenLanguage)
                {
                    NethereumWeb3Version = Constants.NethereumWeb3Version
                };

                //when
                //code gen proj file
                var projectFile = generator.GenerateFileContent(context.TargetProjectFolder);
                GeneratedFileWriter.WriteFileToDisk(projectFile);

                //add in some code gen class files
                var contractMetaData = TestContracts.StandardContract;
                var contractABI = new GeneratorModelABIDeserialiser().DeserialiseABI(contractMetaData.ABI);

                var contractProjectGenerator = new ContractProjectGenerator(
                    contractABI,
                    "StandardContract",
                    contractMetaData.ByteCode,
                    context.ProjectName,
                    "StandardContract.Service",
                    "StandardContract.CQS",
                    "StandardContract.DTO",
                    null,
                    null,
                    context.TargetProjectFolder,
                    Path.DirectorySeparatorChar.ToString(),
                    codeGenLanguage)
                {
                    AddRootNamespaceOnVbProjectsToImportStatements = false
                };

                var generatedFiles = contractProjectGenerator.GenerateAll();
                GeneratedFileWriter.WriteFilesToDisk(generatedFiles);

                context.BuildProject();

                //then
                Assert.True(context.BuildHasSucceeded());
            }
            finally
            {
                context.CleanUp();
            }
        }
    }
}
