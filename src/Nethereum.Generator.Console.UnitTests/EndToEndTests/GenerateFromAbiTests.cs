using System;
using System.Reflection;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.EndToEndTests
{
    public class GenerateFromAbiTests
    {
        [Fact]
        public void GeneratesExpectedFilesAndCompiles()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            var pathToAbi = context.WriteFileToProject("StandardContract.abi", TestData.StandardContract.ABI);

            //when
            const string baseNamespace = "Sample.CodeGenProject";
            var args =
                $"-abi {pathToAbi} -o {context.TargetProjectFolder} -ns {baseNamespace}";
            context.GenerateCode("gen-fromabi", args);
            context.BuildProject();

            //then
            Assert.True(context.DirectoryExists("StandardContract"));
            Assert.True(context.DirectoryExists("StandardContract\\CQS"));
            Assert.True(context.DirectoryExists("StandardContract\\DTO"));
            Assert.True(context.DirectoryExists("StandardContract\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

    }
}
