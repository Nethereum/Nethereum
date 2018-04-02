using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;
using Nethereum.Generators.UnitTests.Expected;
using Xunit;
using FunctionABI = Nethereum.Generators.Model.FunctionABI;

namespace Nethereum.Generators.Tests
{
    public class FunctionCQSMessageGeneratorTests
    {
        [Fact]
        public void GeneratesExpectedFileContent()
        {
            var expectedContent = ExpectedContentRepository.Get("CSharp", "FunctionCQSMessage01.csharp.txt");

            var functionAbi = new FunctionABI("BaseStats", constant: true)
            {
                InputParameters = new[]{new ParameterABI("uint256", "_number")},
                OutputParameters = new[]{new ParameterABI("uint256")}
            };

            var generator = new FunctionCQSMessageGenerator(functionAbi, "DefaultNamespace", "FunctionOutput");
            var fileContent = generator.GenerateFileContent();

            Assert.Equal(expectedContent, fileContent);
        }
    }
}