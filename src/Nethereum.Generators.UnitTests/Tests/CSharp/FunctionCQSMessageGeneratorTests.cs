using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
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

            return new FunctionCQSMessageGenerator(functionAbi, "DefaultNamespace", "FunctionOutput");
        }

        public FunctionCQSMessageGeneratorTests():
            base(CreateGenerator(), "CSharp", "FunctionCQSMessage01.csharp.txt", "BaseStatsFunction.cs")
        {
        }
    }
}