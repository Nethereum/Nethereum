using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.Generator.Console.Commands
{
    public static class Extensions
    {
        public static void AddHelpOption(this CommandLineApplication app)
        {
            app.HelpOption("-? | -h | --help");
        }
    }

}
