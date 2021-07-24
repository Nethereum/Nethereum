using System.Linq;
using Nethereum.Generator.Console.Commands;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.CommandTests
{
    public class GenerateCommandTests
    {
        [Theory]
        [InlineData("Nethereum.Generator.Console.Commands.GenerateFromProjectCommand")]
        [InlineData("Nethereum.Generator.Console.Commands.GenerateFromAbiCommand")]
        public void RegistersExpectedCommands(string fullyQualifedCommandName)
        {
            var app = new GenerateCommand();
            var abiCommand = app.Commands.FirstOrDefault(c => c.ToString() == fullyQualifedCommandName);
            Assert.NotNull(abiCommand);
        }

        [Fact]
        public void SupportsHelpArgs()
        {
            new GenerateCommand().EnsureHelpArgs();
        }
    }
}
