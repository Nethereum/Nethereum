using System;
using System.IO;
using System.Reflection;
using Nethereum.Generators.Core;
using Nethereum.Generators.Net;
using Nethereum.Generators.Tests.Common;
using Nethereum.Generators.UnitTests.TestData;
using Xunit;

namespace Nethereum.Generators.IntegrationTests
{
    public class ContractProjectGeneratorEndToEndTests
    {
        [Theory]
        [InlineData(CodeGenLanguage.CSharp)]
        [InlineData(CodeGenLanguage.FSharp)]
        [InlineData(CodeGenLanguage.Vb)]
        public void GeneratedCodeBuildsSuccessfully(CodeGenLanguage codeGenLanguage)
        {
            //given
            var context = new ProjectTestContext(GetType().Name, $"{MethodBase.GetCurrentMethod().Name}{codeGenLanguage}");

            try
            {
                context.CreateProject(codeGenLanguage, new[]
                {
                    new Tuple<string, string>("Nethereum.Web3", "3.0.0-rc1")
                });

                var contractMetaData = TestContracts.StandardContract;
                var contractABI = new GeneratorModelABIDeserialiser().DeserialiseABI(contractMetaData.ABI);

                //when
                var contractProjectGenerator = new ContractProjectGenerator(
                    contractABI,
                    "StandardContract",
                    contractMetaData.ByteCode,
                    context.ProjectName,
                    "StandardContract.Service",
                    "StandardContract.CQS",
                    "StandardContract.DTO",
                    context.TargetProjectFolder,
                    Path.DirectorySeparatorChar.ToString(),
                    codeGenLanguage)
                {
                    AddRootNamespaceOnVbProjectsToImportStatements = true
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
