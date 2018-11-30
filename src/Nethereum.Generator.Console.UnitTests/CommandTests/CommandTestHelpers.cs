using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using Xunit;

namespace Nethereum.Generator.Console.UnitTests.CommandTests
{
    public static class CommandTestHelpers
    {
        public static void EnsureHelpArgs(this CommandLineApplication command)
        {
            Assert.Equal("-? | -h | --help", command.OptionHelp.Template);
        }

        public static void HasArgs(this CommandLineApplication command, Dictionary<string, string> expectedArgs)
        {
            foreach (var expectedArg in expectedArgs)
            {
                var option = command.Options.FirstOrDefault(x => 
                    x.ShortName == expectedArg.Key && 
                    x.LongName == expectedArg.Value);

                Assert.False(option == null, $"Expected Arg Key: '{expectedArg.Key}' with Short Name: '{expectedArg.Value}' was not found");
            }
        }
    }
}
