using System.Linq;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests
{
    public class AppTests
    {
        [Theory]
        [InlineData("Nethereum.Generator.Console.Commands.GenerateFromProjectCommand")]
        [InlineData("Nethereum.Generator.Console.Commands.GenerateFromAbiCommand")]
        public void RegistersExpectedCommands(string fullyQualifedCommandName)
        {
            var app = new App();
            var abiCommand = app.Commands.FirstOrDefault(c => c.ToString() == fullyQualifedCommandName);
            Assert.NotNull(abiCommand);
        }
    }
}
