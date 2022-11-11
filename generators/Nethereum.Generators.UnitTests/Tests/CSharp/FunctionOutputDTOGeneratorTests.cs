using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;
using Xunit;

namespace Nethereum.Generators.Tests.CSharp
{
    public class FunctionOutputDTOGeneratorTests : GeneratorTestBase<FunctionOutputDTOGenerator>
    {
        static FunctionOutputDTOGenerator CreateGenerator()
        {
            var contractAbi = new ContractABI();
            var functionAbi = new FunctionABI("GetCar", true, contractAbi)
            {
                OutputParameters = new[] {
                    new ParameterABI("uint", order: 1),
                    new ParameterABI("string", order: 2)}
            };
            contractAbi.Functions = new FunctionABI[] { functionAbi };

            return new FunctionOutputDTOGenerator(functionAbi, "DefaultNamespace", CodeGenLanguage.CSharp);
        }

        public FunctionOutputDTOGeneratorTests():base(CreateGenerator(), "CSharp")
        {
        }

        [Fact]
        public override void GeneratesExpectedFileContent()
        {
            GenerateAndCheckFileContent("FunctionOutputDTO.01.csharp.txt");
        }

        [Fact]
        public override void GeneratesExpectedFileName()
        {
            GenerateAndCheckFileName("GetCarOutputDTO.cs");
        }
    }
}
