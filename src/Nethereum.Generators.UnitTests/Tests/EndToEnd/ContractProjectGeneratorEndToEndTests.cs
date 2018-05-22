using Nethereum.Generators.Core;
using Nethereum.Generators.Net;
using Nethereum.Generators.UnitTests.TestData;
using Nethereum.Generators.UnitTests.Tests.EndToEndTests;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests.EndToEnd
{
    public class ContractProjectGeneratorEndToEndTests
    {
        [Fact]
        public void GeneratedCodeBuildsSuccessfully_Csharp()
        {
            GenerateCodeAndBuild(CodeGenLanguage.CSharp);
        }

        [Fact]
        public void GeneratedCodeBuildsSuccessfully_Vb()
        {
            GenerateCodeAndBuild(CodeGenLanguage.Vb);
        }

        [Fact]
        public void GeneratedCodeBuildsSuccessfully_Fsharp()
        {
            GenerateCodeAndBuild(CodeGenLanguage.FSharp);
        }

        private void GenerateCodeAndBuild(CodeGenLanguage codeGenLanguage)
        {
            //given
            var context = new EndToEndTestContext(GetType().Name, $"{MethodBase.GetCurrentMethod().Name}{codeGenLanguage}");
            context.CreateProject(codeGenLanguage, new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
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
                codeGenLanguage);

            var generatedFiles = contractProjectGenerator.GenerateAll();
            GeneratedFileWriter.WriteFilesToDisk(generatedFiles);
            context.BuildProject();

            //then
            Assert.True(context.BuildHasSucceeded());
        }
    }
}
