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
    public class GenerateFromProjectCommandTests
    {
        private readonly GenerateFromProjectCommand _command;
        private readonly Mock<ICodeGenerationWrapper> _mockCodeGenerationWrapper;

        public GenerateFromProjectCommandTests()
        {
            _mockCodeGenerationWrapper = new Mock<ICodeGenerationWrapper>();
            _command = new GenerateFromProjectCommand(){CodeGenerationWrapper = _mockCodeGenerationWrapper.Object};
        }

        [Fact]
        public void InstantiatesDefaultCodeGenerationWrapper()
        {
            Assert.Equal(typeof(CodeGenerationWrapper), new GenerateFromProjectCommand().CodeGenerationWrapper.GetType());
        }

        [Fact]
        public void HasExpectedCommandName()
        {
            Assert.Equal("from-project", _command.Name);
        }

        [Fact]
        public void HasExpectedArgs()
        {
            var expectedArgs = new Dictionary<string, string>
            {
                {"p", "projectPath"},
                {"a", "assemblyName"},
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
                "-p", "c:/Temp/MyProject/MyProject.csproj", 
                "-a", "MyProject.dll"));

            _mockCodeGenerationWrapper
                .Verify(w => w.FromProject("c:/Temp/MyProject/MyProject.csproj", "MyProject.dll"));
        }

        [Fact]
        public void ProjectPathDefaultsToWorkingDirectory()
        {
            Assert.Equal(0, _command.Execute());

            var expectedPath = Environment.CurrentDirectory;
            var expectedAssemblyName =
                expectedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Last();

            _mockCodeGenerationWrapper
                .Verify(w => w.FromProject(expectedPath, expectedAssemblyName));
        }
    }
}
