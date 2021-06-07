using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators;
using Nethereum.Generators.ProtocolBuffers.UnitTests.ExpectedContent;
using Xunit;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.Tests.ABIToProto
{
    public class FunctionABIToProtoGeneratorTests
    {
        [Fact]
        public void GeneratesExpectedProtoBufferContent()
        {
            var contractAbi = new ContractABI();
            var abi = new FunctionABI("recordHousePurchase", false, contractAbi)
            {
                InputParameters = new[]
                {
                    new ParameterABI("bytes32", "propertyId", 1), 
                    new ParameterABI("bytes32", "buyerId", 2),
                    new ParameterABI("uint32", "date", 3),
                    new ParameterABI("uint32", "price", 4),
                },
                OutputParameters = new[]
                {
                    new ParameterABI("uint"),
                }
            };
            contractAbi.Functions = new FunctionABI[] {abi};

            var generator = new FunctionABIToProtoGenerator(abi);
            var actualProtoFileContent = generator.GenerateFileContent();

            var expectedContent = GetExpectedProtoContent("FunctionABIToProto.01.proto");

            Assert.Equal(expectedContent, actualProtoFileContent);
        }

        [Fact]
        public void CanGenerateProtoContentWhenAbiHasNoReturnParameters()
        {
            var contractAbi = new ContractABI();
            var abi = new FunctionABI("recordHousePurchase", false, contractAbi)
            {
                InputParameters = new[]
                {
                    new ParameterABI("bytes32", "propertyId", 1)
                }
            };
            contractAbi.Functions = new FunctionABI[] { abi };

            var generator = new FunctionABIToProtoGenerator(abi);
            var actualProtoFileContent = generator.GenerateFileContent();

            var expectedContent = GetExpectedProtoContent("FunctionABIToProto.02.proto");

            Assert.Equal(expectedContent, actualProtoFileContent);
        }

        [Fact]
        public void GeneratesExpectedFileName()
        {
            var contractAbi = new ContractABI();
            var abi = new FunctionABI("recordHousePurchase", false, contractAbi){};
            contractAbi.Functions = new FunctionABI[] { abi };

            var generator = new FunctionABIToProtoGenerator(abi);
            var actualFileName = generator.GetFileName();

            Assert.Equal("RecordHousePurchaseMessages.proto", actualFileName);
        }

        private string GetExpectedProtoContent(string resourceName)
        {
            return ExpectedContentRepository.Get(
                $"Nethereum.Generators.ProtocolBuffers.UnitTests.ExpectedContent.Proto.{resourceName}");
        }
    }
}
