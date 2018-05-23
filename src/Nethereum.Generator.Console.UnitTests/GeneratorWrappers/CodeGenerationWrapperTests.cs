using Moq;
using Nethereum.Generator.Console.Configuration;
using Nethereum.Generator.Console.Generation;
using Nethereum.Generators.Core;
using Nethereum.Generators.Net;
using Nethereum.Generators.UnitTests.TestData;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.GeneratorWrappers
{
    public class CodeGenerationWrapperTests
    {
        private readonly Mock<IGeneratorConfigurationFactory> _mockGeneratorConfigurationFactory;
        private readonly Mock<IGeneratedFileWriter> _mockGeneratedFileWriter;
        private readonly CodeGenerationWrapper _codeGenerationWrapper;

        public CodeGenerationWrapperTests()
        {
            _mockGeneratorConfigurationFactory = new Mock<IGeneratorConfigurationFactory>();
            _mockGeneratedFileWriter = new Mock<IGeneratedFileWriter>();
            _codeGenerationWrapper = new CodeGenerationWrapper(_mockGeneratorConfigurationFactory.Object, _mockGeneratedFileWriter.Object);
        }

        [Fact] 
        public void FromAbi_CallsConfigFactory_GeneratesCode_SendsToWriter()
        {
            //given
            GeneratorConfiguration stubGeneratorConfiguration = CreateStubConfiguration();

            _mockGeneratorConfigurationFactory
                .Setup(f => f.FromAbi(
                    "StandardContract", "StandardContract.abi", "StandardContract.bin", "DefaultNamespace", "c:/temp"))
                .Returns(stubGeneratorConfiguration);

            IEnumerable<GeneratedFile> actualFilesSentToWriter = null;

            _mockGeneratedFileWriter
                .Setup(f => f.WriteFiles(It.IsAny<IEnumerable<GeneratedFile>>()))
                .Callback<IEnumerable<GeneratedFile>>((files) => actualFilesSentToWriter = files);

            //when
            _codeGenerationWrapper.FromAbi("StandardContract", "StandardContract.abi", "StandardContract.bin", "DefaultNamespace", "c:/temp");

            //then
            Assert.NotNull(actualFilesSentToWriter);
            Assert.True(actualFilesSentToWriter.ToArray().Length > 0);
        }


        [Fact]
        public void FromProject_CallsConfigFactory_GeneratesCode_SendsToWriter()
        {
            //given
            GeneratorConfiguration stubGeneratorConfiguration = CreateStubConfiguration();

            _mockGeneratorConfigurationFactory
                .Setup(f => f.FromProject(
                    "c:/temp/projectx", "CompanyA.ProjectX.dll"))
                .Returns(stubGeneratorConfiguration);

            IEnumerable<GeneratedFile> actualFilesSentToWriter = null;

            _mockGeneratedFileWriter
                .Setup(f => f.WriteFiles(It.IsAny<IEnumerable<GeneratedFile>>()))
                .Callback<IEnumerable<GeneratedFile>>((files) => actualFilesSentToWriter = files);

            //when
            _codeGenerationWrapper.FromProject("c:/temp/projectx", "CompanyA.ProjectX.dll");

            //then
            Assert.NotNull(actualFilesSentToWriter);
            Assert.True(actualFilesSentToWriter.ToArray().Length > 0);
        }

        private static GeneratorConfiguration CreateStubConfiguration()
        {
            return new GeneratorConfiguration
            {
                ABIConfigurations = new List<ABIConfiguration>
                {
                    new ABIConfiguration
                    {
                        ABI = TestContracts.StandardContract.ABI,
                        ByteCode = TestContracts.StandardContract.ByteCode,
                        ContractName = "StandardContract",
                        BaseOutputPath = "c:/Temp",
                        BaseNamespace = "DefaultNamespace",
                        ServiceNamespace = "StandardContract.Service",
                        DTONamespace = "StandardContract.DTO",
                        CQSNamespace = "StandardContract.CQS",
                        CodeGenLanguage = CodeGenLanguage.CSharp
                    }
                }
            };
        }
    }
}
