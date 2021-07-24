using System;
using Moq;
using Nethereum.Generator.Console.Commands;
using Nethereum.Generator.Console.Generation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.CommandTests
{
    public class GenerateFromTruffleCommandTests
    {
        private readonly GenerateFromTruffleCommand _command;
        private readonly Mock<ICodeGenerationWrapper> _mockCodeGenerationWrapper;

        public GenerateFromTruffleCommandTests()
        {
            _mockCodeGenerationWrapper = new Mock<ICodeGenerationWrapper>();
            _command = new GenerateFromTruffleCommand()
            {
                CodeGenerationWrapper = _mockCodeGenerationWrapper.Object
            };
        }

        [Fact]
        public void InstantiatesDefaultCodeGenerationWrapper()
        {
            Assert.Equal(typeof(CodeGenerationWrapper), 
                new GenerateFromProjectCommand().CodeGenerationWrapper.GetType());
        }

        [Fact]
        public void HasExpectedCommandName()
        {
            Assert.Equal("from-truffle", _command.Name);
        }

        [Fact]
        public void HasExpectedArgs()
        {
            var expectedArgs = new Dictionary<string, string>
            {
                {"d", "directory"},
                {"o", "outputPath"},
                {"ns", "namespace"},
                {"sf", "singleFile"}
            };

            _command.HasArgs(expectedArgs);
        }

        [Fact]
        public void SupportsHelpArgs()
        {
            _command.EnsureHelpArgs();
        }

        [Fact]
        public void ExecuteInvokesCodeGeneratorWithExpectedValues()
        {
            Assert.Equal(0, _command.Execute(
                "-d", "c:/Temp/MyTruffleProject/build/contracts", 
                "-ns", "Acme.Ethereum.Contracts",
                "-o", "c:/Temp/MyDotNetProject/generated",
                "-sf", "true"));

            _mockCodeGenerationWrapper
                .Verify(w => w.FromTruffle(
                    "c:/Temp/MyTruffleProject/build/contracts", 
                    "Acme.Ethereum.Contracts",
                    "c:/Temp/MyDotNetProject/generated",
                    true));
        }

        [Fact]
        public void SingleFileArgIsOptional_DefaultsToTrue()
        {
            Assert.Equal(0, _command.Execute(
                "-d", "c:/Temp/MyTruffleProject/build/contracts", 
                "-ns", "Acme.Ethereum.Contracts",
                "-o", "c:/Temp/MyDotNetProject/generated"));

            _mockCodeGenerationWrapper
                .Verify(w => w.FromTruffle(
                    "c:/Temp/MyTruffleProject/build/contracts", 
                    "Acme.Ethereum.Contracts",
                    "c:/Temp/MyDotNetProject/generated",
                    true));
        }

        [Fact]
        public void DirectoryArgIsMandatory()
        {
            Assert.Equal(1, _command.Execute(
                "-ns", "Acme.Ethereum.Contracts",
                "-o", "c:/Temp/MyDotNetProject/generated",
                "-sf", "true"
                ));
        }

        [Fact]
        public void NamespaceArgIsMandatory()
        {
            Assert.Equal(1, _command.Execute(
                "-d", "c:/Temp/MyTruffleProject/build/contracts", 
                "-o", "c:/Temp/MyDotNetProject/generated",
                "-sf", "true"));
        }

        [Fact]
        public void OutputPathArgIsMandatory()
        {
            Assert.Equal(1, _command.Execute(
                "-d", "c:/Temp/MyTruffleProject/build/contracts", 
                "-ns", "Acme.Ethereum.Contracts",
                "-sf", "true"));
        }
    }
}
