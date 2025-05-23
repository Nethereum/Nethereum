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
        private const string BaseName = "ContractProjGenE2ETests";

        [Theory]
        [InlineData(CodeGenLanguage.CSharp)]
        [InlineData(CodeGenLanguage.FSharp)]
        [InlineData(CodeGenLanguage.Vb)]
        public void Generated_Code_Builds(CodeGenLanguage codeGenLanguage)
        {
            //given
            var context = new ProjectTestContext(BaseName, 
                $"{MethodBase.GetCurrentMethod().Name}{codeGenLanguage}");

            try
            {
                context.CreateProject(codeGenLanguage, new[]
                {
                    new Tuple<string, string>("Nethereum.Web3", Constants.NethereumWeb3Version)
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
                    null,
                    null,
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

        [Theory]
        [InlineData(CodeGenLanguage.CSharp)]
        //[InlineData(CodeGenLanguage.FSharp)]
        //[InlineData(CodeGenLanguage.Vb)]
        public void With_Single_Messages_File_Generated_Code_Builds(CodeGenLanguage codeGenLanguage)
        {
            //given
            var context = new ProjectTestContext(BaseName, 
                $"{MethodBase.GetCurrentMethod().Name}{codeGenLanguage}");

            try
            {
                //context.CreateProject(codeGenLanguage, new[]
                //{
                //    new Tuple<string, string>("Nethereum.Web3", Constants.NethereumWeb3Version)
                //});

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
                    null,
                    null,
                    context.TargetProjectFolder,
                    Path.DirectorySeparatorChar.ToString(),
                    codeGenLanguage)
                {
                    AddRootNamespaceOnVbProjectsToImportStatements = true,
                };

                var generatedFiles = contractProjectGenerator.GenerateAllMessagesFileAndService();
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
