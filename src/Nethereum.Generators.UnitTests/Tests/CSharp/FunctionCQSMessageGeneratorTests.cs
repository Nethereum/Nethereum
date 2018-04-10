using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Xunit;
using FunctionABI = Nethereum.Generators.Model.FunctionABI;

namespace Nethereum.Generators.Tests.CSharp
{
    public class FunctionCQSMessageGeneratorTests: GeneratorTestBase<FunctionCQSMessageGenerator>
    {
        static FunctionCQSMessageGenerator CreateGenerator()
        {
            var functionAbi = new FunctionABI("BaseStats", constant: true)
            {
                InputParameters = new[] { new ParameterABI("uint256", "_number") },
                OutputParameters = new[] { new ParameterABI("uint256") }
            };

            return new FunctionCQSMessageGenerator(functionAbi, "DefaultNamespace", "FunctionOutput", CodeGenLanguage.CSharp);
        }

        public FunctionCQSMessageGeneratorTests():base(CreateGenerator(), "CSharp")
        {
        }

        [Fact]
        public override void GeneratesExpectedFileContent()
        {
            GenerateAndCheckFileContent("FunctionCQSMessage.01.csharp.txt");
        }

        [Fact]
        public override void GeneratesExpectedFileName()
        {
            GenerateAndCheckFileName("BaseStatsFunction.cs");
        }
    }
}