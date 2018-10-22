using System.Linq;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests
{
    public class AppTests
    {
        [Theory]
        [InlineData("Nethereum.Generator.Console.Commands.GenerateCommand")]
        public void RegistersExpectedCommands(string fullyQualifedCommandName)
        {
            var app = new App();
            var command = app.Commands.FirstOrDefault(c => c.ToString() == fullyQualifedCommandName);
            Assert.NotNull(command);
        }
    }
}
