using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generators.Net;
using System.IO;
using System;

namespace Nethereum.Generator.Console.Commands
{
    public class GenerateCommand: CommandLineApplication
    {        
        public GenerateCommand()
        {
            Name = "generate";
            Description = "Generates Nethereum code for Ethereum integration and interaction.";

            Commands.Add(new GenerateFromProjectCommand());
            Commands.Add(new GenerateFromAbiCommand());
            Commands.Add(new GenerateFromTruffleCommand());
            Commands.Add(new GenerateFromConfigCommand());

            this.AddHelpOption();
        }

       
    }
}
