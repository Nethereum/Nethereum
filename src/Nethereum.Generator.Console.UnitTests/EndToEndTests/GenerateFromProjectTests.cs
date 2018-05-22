using Nethereum.Generator.Console.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.EndToEndTests
{
    public class GenerateFromProjectTests
    {
        [Fact]
        public void FromAbiFilesInProject()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            //abi in project root
            context.WriteFileToProject("StandardContract1.abi", TestData.StandardContract.ABI);
            //abi in sub folder
            context.WriteFileToProject("Solidity\\StandardContract2.abi", TestData.StandardContract.ABI);

            //when
            var args =
                $"-p {context.ProjectFilePath} -a {context.OutputAssemblyName}";

            context.GenerateCode("gen-fromproject", args);
            context.BuildProject();

            //then
            Assert.True(context.DirectoryExists("StandardContract1"));
            Assert.True(context.DirectoryExists("StandardContract1\\CQS"));
            Assert.True(context.DirectoryExists("StandardContract1\\DTO"));
            Assert.True(context.DirectoryExists("StandardContract1\\Service"));
            Assert.True(context.DirectoryExists("StandardContract2"));
            Assert.True(context.DirectoryExists("StandardContract2\\CQS"));
            Assert.True(context.DirectoryExists("StandardContract2\\DTO"));
            Assert.True(context.DirectoryExists("StandardContract2\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

        [Fact]
        public void FromAbiFilesInProjectSubFolders()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            //abi in project root
            context.WriteFileToProject("StandardContract1.abi", TestData.StandardContract.ABI);
            //abi in sub folder
            context.WriteFileToProject("Solidity\\StandardContract2.abi", TestData.StandardContract.ABI);

            //when
            var args =
                $"-p {context.TargetProjectFolder} -a {context.OutputAssemblyName}";

            context.GenerateCode("gen-fromproject", args);
            context.BuildProject();

            //then
            Assert.True(context.DirectoryExists("StandardContract1"));
            Assert.True(context.DirectoryExists("StandardContract1\\CQS"));
            Assert.True(context.DirectoryExists("StandardContract1\\DTO"));
            Assert.True(context.DirectoryExists("StandardContract1\\Service"));
            Assert.True(context.DirectoryExists("StandardContract2"));
            Assert.True(context.DirectoryExists("StandardContract2\\CQS"));
            Assert.True(context.DirectoryExists("StandardContract2\\DTO"));
            Assert.True(context.DirectoryExists("StandardContract2\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

        [Fact]
        public void FromConfigContainingAbiContent()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            var config = new GeneratorConfiguration
            {
                ABIConfigurations = new List<ABIConfiguration>
                {
                    new ABIConfiguration
                    {
                        ContractName = "StandardContractA",
                        ABI = TestData.StandardContract.ABI
                    }
                }
            };

            config.SaveToJson(context.TargetProjectFolder);

            //when
            var args =
                $"-p {context.ProjectFilePath} -a {context.OutputAssemblyName}";

            context.GenerateCode("gen-fromproject", args);
            context.BuildProject();

            //then
            Assert.True(context.DirectoryExists("StandardContractA"));
            Assert.True(context.DirectoryExists("StandardContractA\\CQS"));
            Assert.True(context.DirectoryExists("StandardContractA\\DTO"));
            Assert.True(context.DirectoryExists("StandardContractA\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

        [Fact]
        public void WhenConfigIsPresentCodeGenIsOnlyForAbisInConfig()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            var config = new GeneratorConfiguration
            {
                ABIConfigurations = new List<ABIConfiguration>
                {
                    new ABIConfiguration
                    {
                        ContractName = "StandardContractA",
                        ABI = TestData.StandardContract.ABI
                    }
                }
            };

            //generator config with StandardContractA
            config.SaveToJson(context.TargetProjectFolder);

            //abi in project called StandardContractB
            context.WriteFileToProject("StandardContractB.abi", TestData.StandardContract.ABI);

            //when
            var args =
                $"-p {context.ProjectFilePath} -a {context.OutputAssemblyName}";

            context.GenerateCode("gen-fromproject", args);
            context.BuildProject();

            //then
            Assert.False(context.DirectoryExists("StandardContractB"));
            Assert.True(context.DirectoryExists("StandardContractA"));
            Assert.True(context.DirectoryExists("StandardContractA\\CQS"));
            Assert.True(context.DirectoryExists("StandardContractA\\DTO"));
            Assert.True(context.DirectoryExists("StandardContractA\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

        [Fact]
        public void FromConfigWithAbsoluteAbiPath()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            var abiPath = Path.Combine(Path.GetTempPath(), "StandardContract.abi");
            File.WriteAllText(abiPath, TestData.StandardContract.ABI, Encoding.UTF8);

            var config = new GeneratorConfiguration
            {
                ABIConfigurations = new List<ABIConfiguration>
                {
                    new ABIConfiguration
                    {
                        ABIFile = abiPath
                    }
                }
            };

            config.SaveToJson(context.TargetProjectFolder);

            //when
            var args =
                $"-p {context.ProjectFilePath} -a {context.OutputAssemblyName}";

            context.GenerateCode("gen-fromproject", args);
            context.BuildProject();

            //then
            Assert.True(context.DirectoryExists("StandardContract"));
            Assert.True(context.DirectoryExists("StandardContract\\CQS"));
            Assert.True(context.DirectoryExists("StandardContract\\DTO"));
            Assert.True(context.DirectoryExists("StandardContract\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

        [Fact]
        public void FromConfigWithProjectRelativeAbiPath()
        {
            //given
            var context = new EndToEndTestContext(
                this.GetType().Name, MethodBase.GetCurrentMethod().Name);

            context.CreateProject(new []
            {
                new Tuple<string, string>("Nethereum.Web3", "2.4.0")
            });

            context.WriteFileToProject("solidity\\StandardContractA.abi", TestData.StandardContract.ABI);

            var config = new GeneratorConfiguration
            {
                ABIConfigurations = new List<ABIConfiguration>
                {
                    new ABIConfiguration
                    {
                        ABIFile = "solidity\\StandardContractA.abi"
                    }
                }
            };

            config.SaveToJson(context.TargetProjectFolder);

            //when
            var args =
                $"-p {context.ProjectFilePath} -a {context.OutputAssemblyName}";

            context.GenerateCode("gen-fromproject", args);
            context.BuildProject();

            //then
            Assert.True(context.DirectoryExists("StandardContractA"));
            Assert.True(context.DirectoryExists("StandardContractA\\CQS"));
            Assert.True(context.DirectoryExists("StandardContractA\\DTO"));
            Assert.True(context.DirectoryExists("StandardContractA\\Service"));
            Assert.True(context.BuildHasSucceeded());

            context.CleanUp();
        }

    }
}
