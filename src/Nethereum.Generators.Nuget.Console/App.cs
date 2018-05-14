using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;

namespace Nethereum.Generators.Nuget.Console
{
    public class App : CommandLineApplication
    {
        public App()
        {
            Commands.Add(new GenerateCommand());
            HelpOption("-h | -? | --help");
        }
    }
}
