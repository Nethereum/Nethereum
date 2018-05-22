using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nethereum.Generator.Console.Configuration;
using Nethereum.Generator.Console.UnitTests.EndToEndTests;
using Nethereum.Generators.Core;
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
            var context = new EndToEndTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            context.CreateProject();

            context.WriteFileToProject("StandardContract.abi", TestData.StandardContract.ABI);
            context.WriteFileToProject("StandardContract.bin", TestData.StandardContract.ByteCode);

            //when
            var config = factory.FromAbi(
                "StandardContract",  
                "StandardContract.abi", 
                "StandardContract.bin", 
                Path.GetFileNameWithoutExtension(context.OutputAssemblyName), 
                context.TargetProjectFolder);

            //then
            Assert.Equal(1, config?.ABIConfigurations?.Count);
            var abiConfig = config.ABIConfigurations.First();
            Assert.NotNull(abiConfig);
            Assert.Equal(CodeGenLanguage.CSharp, abiConfig.CodeGenLanguage);
            Assert.Equal("StandardContract", abiConfig.ContractName);
            Assert.Equal(TestData.StandardContract.ABI, abiConfig.ABI);
            Assert.Equal(TestData.StandardContract.ByteCode, abiConfig.ByteCode);
            Assert.Equal(context.TargetProjectFolder, abiConfig.BaseOutputPath);
            Assert.Equal(Path.GetFileNameWithoutExtension(context.OutputAssemblyName), abiConfig.BaseNamespace);
            Assert.Equal("StandardContract.CQS", abiConfig.CQSNamespace);
            Assert.Equal("StandardContract.DTO", abiConfig.DTONamespace);
            Assert.Equal("StandardContract.Service", abiConfig.ServiceNamespace);
        }

        [Fact]
        public void WhenBinFileIsNotSpecifiedItIsFoundByConvention()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new EndToEndTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            context.CreateProject();

            context.WriteFileToProject("StandardContract.abi", TestData.StandardContract.ABI);
            context.WriteFileToProject("StandardContract.bin", TestData.StandardContract.ByteCode);

            //when
            var config = factory.FromAbi(
                "StandardContract",  
                "StandardContract.abi", 
                null, // bin file
                Path.GetFileNameWithoutExtension(context.OutputAssemblyName), 
                context.TargetProjectFolder);

            //then
            Assert.Equal(1, config?.ABIConfigurations?.Count);
            var abiConfig = config.ABIConfigurations.First();
            Assert.Equal(TestData.StandardContract.ByteCode, abiConfig.ByteCode);
        }

        [Fact]
        public void WhenContractNameIsNotSpecifiedUsesAbiFileName()
        {
            //given
            var factory = new GeneratorConfigurationFactory();
            var context = new EndToEndTestContext(this.GetType().Name, MethodBase.GetCurrentMethod().Name);
            context.CreateProject();

            context.WriteFileToProject("StandardContract.abi", TestData.StandardContract.ABI);
            context.WriteFileToProject("StandardContract.bin", TestData.StandardContract.ByteCode);

            //when
            var config = factory.FromAbi(
                null,  //contract name
                "StandardContract.abi", 
                "StandardContract.bin", 
                Path.GetFileNameWithoutExtension(context.OutputAssemblyName), 
                context.TargetProjectFolder);

            //then
            Assert.Equal(1, config?.ABIConfigurations?.Count);
            var abiConfig = config.ABIConfigurations.First();
            Assert.Equal("StandardContract", abiConfig.ContractName);
        }
    }
}