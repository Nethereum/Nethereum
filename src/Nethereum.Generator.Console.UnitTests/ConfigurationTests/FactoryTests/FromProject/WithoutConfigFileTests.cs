using Nethereum.Generator.Console.Configuration;
using Nethereum.Generators.Core;
using Nethereum.Generators.Tests.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Nethereum.Generators.UnitTests.TestData;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.ConfigurationTests.FactoryTests.FromProject
{
    public class WithoutConfigFileTests
    {
        public enum PathType
        {
            FolderAndFile,
            FolderOnly
        }

        [Theory]
        [InlineData(PathType.FolderAndFile)]
        [InlineData(PathType.FolderOnly)]
        public void GivenProjectFileOrFolderPath_ItFindsAllAbiFilesInProjectFolder(PathType pathType)
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
                var path = pathType == PathType.FolderAndFile ? context.ProjectFilePath : context.TargetProjectFolder;
                var config = factory.FromProject(path, context.OutputAssemblyName);

                //then
                Assert.Equal(1, config?.ABIConfigurations?.Count);
                var abiConfig = config.ABIConfigurations.First();
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
    }
}