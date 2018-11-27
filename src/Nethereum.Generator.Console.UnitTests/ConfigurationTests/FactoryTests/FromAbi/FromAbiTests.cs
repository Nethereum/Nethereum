using Nethereum.Generator.Console.Configuration;
using Nethereum.Generators.Core;
using Nethereum.Generators.Tests.Common;
using Nethereum.Generators.UnitTests.TestData;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests.FactoryTests.FromAbi
{
    public class FromAbiTests
    {
        [Fact]
        public void GeneratesConfigurationFromAbiFilesInProject()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new ProjectTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                context.CreateProject();

                context.WriteFileToProject("StandardContract.abi", TestContracts.StandardContract.ABI);
                context.WriteFileToProject("StandardContract.bin", TestContracts.StandardContract.ByteCode);

                //when
                var config = factory.FromAbi(
                    "StandardContract",
                    "StandardContract.abi",
                    "StandardContract.bin",
                    Path.GetFileNameWithoutExtension(context.OutputAssemblyName),
                    context.TargetProjectFolder);

                //then
                Assert.Equal(1, config?.Contracts?.Count);
                var abiConfig = config.Contracts.First();
                Assert.NotNull(abiConfig);
                Assert.Equal(CodeGenLanguage.CSharp, abiConfig.CodeGenLanguage);
                Assert.Equal("StandardContract", abiConfig.ContractName);
                Assert.Equal(TestContracts.StandardContract.ABI, abiConfig.ABI);
                Assert.Equal(TestContracts.StandardContract.ByteCode, abiConfig.ByteCode);
                Assert.Equal(context.TargetProjectFolder, abiConfig.BaseOutputPath);
                Assert.Equal(Path.GetFileNameWithoutExtension(context.OutputAssemblyName), abiConfig.BaseNamespace);
                Assert.Equal("StandardContract.CQS", abiConfig.CQSNamespace);
                Assert.Equal("StandardContract.DTO", abiConfig.DTONamespace);
                Assert.Equal("StandardContract.Service", abiConfig.ServiceNamespace);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void WhenBinFileIsNotSpecifiedItIsFoundByConvention()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new ProjectTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                context.CreateProject();

                context.WriteFileToProject("StandardContract.abi", TestContracts.StandardContract.ABI);
                context.WriteFileToProject("StandardContract.bin", TestContracts.StandardContract.ByteCode);

                //when
                var config = factory.FromAbi(
                    "StandardContract",
                    "StandardContract.abi",
                    null, // bin file
                    Path.GetFileNameWithoutExtension(context.OutputAssemblyName),
                    context.TargetProjectFolder);

                //then
                Assert.Equal(1, config?.Contracts?.Count);
                var abiConfig = config.Contracts.First();
                Assert.Equal(TestContracts.StandardContract.ByteCode, abiConfig.ByteCode);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void WhenContractNameIsNotSpecifiedUsesAbiFileName()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new ProjectTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            try
            {
                context.CreateProject();

                context.WriteFileToProject("StandardContract.abi", TestContracts.StandardContract.ABI);
                context.WriteFileToProject("StandardContract.bin", TestContracts.StandardContract.ByteCode);

                //when
                var config = factory.FromAbi(
                    null, //contract name
                    "StandardContract.abi",
                    "StandardContract.bin",
                    Path.GetFileNameWithoutExtension(context.OutputAssemblyName),
                    context.TargetProjectFolder);

                //then
                Assert.Equal(1, config?.Contracts?.Count);
                var abiConfig = config.Contracts.First();
                Assert.Equal("StandardContract", abiConfig.ContractName);
            }
            finally
            {
                context.CleanUp();
            }
        }
    }
}