using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nethereum.Generator.Console.Configuration;
using Nethereum.Generators.Core;
using Nethereum.Generators.Tests.Common;
using Nethereum.Generators.UnitTests.TestData;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests.FactoryTests.FromProject
{
    public class WithConfigFileTests
    {
        [Fact]
        public void GivenRelativeAbiFilePathInProject()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new ProjectTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                context.CreateProject();

                context.WriteFileToProject("solidity\\StandardContract.abi", TestContracts.StandardContract.ABI);
                context.WriteFileToProject("solidity\\StandardContract.bin", TestContracts.StandardContract.ByteCode);

                var generatorConfig = new GeneratorConfiguration
                {
                    ABIConfigurations = new List<ABIConfiguration>
                    {
                        new ABIConfiguration
                        {
                            ContractName = "StandardContractA",
                            ABIFile = "solidity\\StandardContract.abi"
                        }
                    }
                };

                generatorConfig.SaveToJson(context.TargetProjectFolder);

                //when
                var config = factory.FromProject(context.TargetProjectFolder, context.OutputAssemblyName);

                //then
                Assert.Equal(1, config?.ABIConfigurations?.Count);
                var abiConfig = config.ABIConfigurations.First();
                Assert.NotNull(abiConfig);
                Assert.Equal(CodeGenLanguage.CSharp, abiConfig.CodeGenLanguage);
                Assert.Equal("StandardContractA", abiConfig.ContractName);
                Assert.Equal(TestContracts.StandardContract.ABI, abiConfig.ABI);
                Assert.Equal(TestContracts.StandardContract.ByteCode, abiConfig.ByteCode);
                Assert.Equal(context.TargetProjectFolder, abiConfig.BaseOutputPath);
                Assert.Equal(Path.GetFileNameWithoutExtension(context.OutputAssemblyName), abiConfig.BaseNamespace);
                Assert.Equal("StandardContractA.CQS", abiConfig.CQSNamespace);
                Assert.Equal("StandardContractA.DTO", abiConfig.DTONamespace);
                Assert.Equal("StandardContractA.Service", abiConfig.ServiceNamespace);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void GivenRelativeAbiPathInProjectParent()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new ProjectTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                context.CreateProject();

                context.WriteFileToProject("..\\StandardContract.abi", TestContracts.StandardContract.ABI);

                var generatorConfig = new GeneratorConfiguration
                {
                    ABIConfigurations = new List<ABIConfiguration>
                    {
                        new ABIConfiguration
                        {
                            ContractName = "StandardContractA",
                            ABIFile = "..\\StandardContract.abi"
                        }
                    }
                };

                generatorConfig.SaveToJson(context.TargetProjectFolder);

                //when
                var config = factory.FromProject(context.TargetProjectFolder, context.OutputAssemblyName);

                //then
                Assert.Equal(1, config?.ABIConfigurations?.Count);
                var abiConfig = config.ABIConfigurations.First();
                Assert.NotNull(abiConfig);
                Assert.Equal(CodeGenLanguage.CSharp, abiConfig.CodeGenLanguage);
                Assert.Equal("StandardContractA", abiConfig.ContractName);
                Assert.Equal(TestContracts.StandardContract.ABI, abiConfig.ABI);
            }
            finally
            {
                context.CleanUp();
            }
        }
    }
}