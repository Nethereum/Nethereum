using Moq;
using Nethereum.Generator.Console.Commands;
using Nethereum.Generator.Console.Generation;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.CommandTests
{
    public class GenerateFromAbiCommandTests
    {
        private readonly GenerateFromAbiCommand _command;
        private readonly Mock<ICodeGenerationWrapper> _mockCodeGenerationWrapper;

        public GenerateFromAbiCommandTests()
        {
            _mockCodeGenerationWrapper = new Mock<ICodeGenerationWrapper>();
            _command = new GenerateFromAbiCommand(){CodeGenerationWrapper = _mockCodeGenerationWrapper.Object};
        }

        [Fact]
        public void InstantiatesDefaultCodeGenerationWrapper()
        {
            Assert.Equal(typeof(CodeGenerationWrapper), new GenerateFromAbiCommand().CodeGenerationWrapper.GetType());
        }

        [Fact]
        public void HasExpectedCommandName()
        {
            Assert.Equal("gen-fromabi", _command.Name);
        }

        [Fact]
        public void HasExpectedArgs()
        {
            var expectedArgs = new Dictionary<string, string>
            {
                {"cn", "contractName"},
                {"abi", "abiPath"},
                {"bin", "binPath"},
                {"o", "outputPath"},
                {"ns", "namespace"}
            };

            foreach (var expectedArg in expectedArgs)
            {
                Assert.NotNull(_command.Options.FirstOrDefault(x => x.ShortName == expectedArg.Key && x.LongName == expectedArg.Value));
            }
        }

        [Fact]
        public void ExecuteInvokesCodeGeneratorWithExpectedValues()
        {
            Assert.Equal(0, _command.Execute(
                "-cn", "StandardContract", 
                "-abi", "StandardContract.abi", 
                "-bin", "StandardContract.bin", 
                "-o", "c:/Temp", 
                "-ns", "DefaultNamespace"));

            _mockCodeGenerationWrapper
                .Verify(w => w.FromAbi("StandardContract", "StandardContract.abi", "StandardContract.bin", "DefaultNamespace", "c:/Temp"));
        }

        [Fact]
        public void ContractNameIsOptional()
        {
            Assert.Equal(0, _command.Execute(
                "-cn", string.Empty, 
                "-abi", "StandardContract", 
                "-bin", "StandardContract.bin", 
                "-o", "c:/Temp", 
                "-ns", "DefaultNamespace"));
        }

        [Fact]
        public void AbiFileIsMandatory()
        {
            Assert.Equal(1, _command.Execute(
                "-cn", "StandardContract", 
                "-abi", string.Empty, 
                "-bin", "StandardContract.bin", 
                "-o", "c:/Temp", 
                "-ns", "DefaultNamespace"));
        }

        [Fact]
        public void OuputPathIsMandatory()
        {
            Assert.Equal(1, _command.Execute(
                "-cn", "StandardContract", 
                "-abi", "StandardContract.abi", 
                "-bin", "StandardContract.bin", 
                "-o", null, 
                "-ns", "DefaultNamespace"));
        }

        [Fact]
        public void NamespaceIsMandatory()
        {
            Assert.Equal(1, _command.Execute(
                "-cn", "StandardContract", 
                "-abi", string.Empty, 
                "-bin", "StandardContract.bin", 
                "-o", "c:/Temp", 
                "-ns", null));
        }
    }
}
