using Nethereum.Generators.Model;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.Generators;
using Nethereum.Generators.ProtocolBuffers.UnitTests.ExpectedContent;
using Xunit;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.Tests.ABIToProto
{
    public class ContractABIToProtoGeneratorTests
    {
        [Fact]
        public void GeneratesExpectedProtoBufferContent()
        {
            var constructorAbi = new ConstructorABI()
            {
                InputParameters = new[]
                {
                    new ParameterABI("bytes32", "ownerId", 1)
                }
            };

            var functionAbi1 = new FunctionABI("recordHousePurchase", false)
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

            var eventAbi1 = new EventABI("HousePurchased")
            {
                InputParameters = new[]
                {
                    new ParameterABI("int32", "purchaseId", 1),
                    new ParameterABI("bytes32", "propertyId", 2),
                    new ParameterABI("bytes32", "buyerId", 3),
                    new ParameterABI("uint32", "date", 4),
                    new ParameterABI("uint32", "price", 5),
                }
            };

            var contractABI = new ContractABI
            {
                Constructor = constructorAbi,
                Functions = new[] {functionAbi1},
                Events = new[] {eventAbi1}
            };

            var generator = new ContractABIToProtoGenerator(contractABI, "Proxy.Ethereum.Samples.HousePurchase", "HousePurchase");
            var actualProtoFileContent = generator.GenerateFileContent();

            var expectedContent = GetExpectedProtoContent("ContractABIToProto.01.proto");

            Assert.Equal(expectedContent, actualProtoFileContent);
        }

        [Fact]
        public void GeneratesExpectedFileName()
        {
            var abi = new ContractABI
            {
                Constructor = new ConstructorABI(),
                Functions = new FunctionABI[0],
                Events = new EventABI[0]
            };

            var generator = new ContractABIToProtoGenerator(abi, "DefaultNamespace", "RecordHousePurchaseMessages");
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
