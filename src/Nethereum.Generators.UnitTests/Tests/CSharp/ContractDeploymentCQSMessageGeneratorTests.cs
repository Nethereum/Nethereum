using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Xunit;

namespace Nethereum.Generators.Tests.CSharp
{
    public class ContractDeploymentCQSMessageGeneratorTests: GeneratorTestBase<ContractDeploymentCQSMessageGenerator>
    {
        static ContractDeploymentCQSMessageGenerator CreateGenerator()
        {
            var constructorAbi = new ConstructorABI { InputParameters = new[] { new ParameterABI("uint256", "totalSupply") } };

            return new ContractDeploymentCQSMessageGenerator(
                constructorAbi, namespaceName: "DefaultNamespace", byteCode: "0x123456789", contractName: "StandardContract", codeGenLanguage: CodeGenLanguage.CSharp);
        }

        public ContractDeploymentCQSMessageGeneratorTests():
            base(CreateGenerator(), "CSharp")
        {
        }

        [Fact]
        public override void GeneratesExpectedFileContent()
        {
            GenerateAndCheckFileContent("ContractDeploymentCqsMessage.01.csharp.txt");
        }

        [Fact]
        public override void GeneratesExpectedFileName()
        {
            GenerateAndCheckFileName("StandardContractDeployment.cs");
        }
    }


}