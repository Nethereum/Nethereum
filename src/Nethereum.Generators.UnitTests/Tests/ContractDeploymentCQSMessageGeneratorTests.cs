using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.UnitTests.Expected;
using Xunit;

namespace Nethereum.Generators.Tests
{
    public class ContractDeploymentCQSMessageGeneratorTests
    {
        private readonly ContractDeploymentCQSMessageGenerator _contractDeploymentCqsMessageGenerator;

        public ContractDeploymentCQSMessageGeneratorTests()
        {
            var constructorAbi = new ConstructorABI{ InputParameters = new[] { new ParameterABI("uint256", "totalSupply")}};

            _contractDeploymentCqsMessageGenerator = new ContractDeploymentCQSMessageGenerator(
                constructorAbi, namespaceName: "DefaultNamespace", byteCode: "0x123456789", contractName: "StandardContract");
        }

        [Fact]
        public void GeneratesExpectedFileContent()
        {
            string expectedContent = ExpectedContentRepository.Get("CSharp", "ContractDeploymentCqsMessage01.csharp.txt");
            var fileContent = _contractDeploymentCqsMessageGenerator.GenerateFileContent();
            Assert.Equal(expectedContent, fileContent);
        }
    }
}