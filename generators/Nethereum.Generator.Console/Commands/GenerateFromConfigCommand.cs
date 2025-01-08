using Microsoft.Extensions.CommandLineUtils;
using Nethereum.Generators.Net;
using System.IO;
using System;

namespace Nethereum.Generator.Console.Commands
{
    public class GenerateFromConfigCommand : CommandLineApplication
    {
        private readonly CommandOption _configFilePath;
        private readonly CommandOption _rootPath;
        public GeneratorSetProcessor GeneratorSetProcessor { get; set; }

        public GenerateFromConfigCommand()
        {
            Name = "from-config";
            Description = "Generates Nethereum code based on a JSON configuration file. Defaults to .nethereum-gen.multisettings (used in VSCode Solidity) if no file is specified.";

            // Config file path option
            _configFilePath = Option(
                "-cfg | --configPath",
                "The path to the JSON configuration file (Optional, defaults to .nethereum-gen.multisettings in the current directory)",
                CommandOptionType.SingleValue
            );

            // Root path option
            _rootPath = Option(
                "-r | --rootPath",
                "The root path for file output (Optional, defaults to the current directory)",
                CommandOptionType.SingleValue
            );

            OnExecute((Func<int>)RunCommand);
            GeneratorSetProcessor = new GeneratorSetProcessor();

            this.AddHelpOption();
        }

        private int RunCommand()
        {
            // Default configuration file name
            const string DefaultConfigFileName = ".nethereum-gen.multisettings";

            // Get the configuration file path or default to `.nethereum-gen.multisettings`
            var configFilePath = _configFilePath.HasValue()
                ? _configFilePath.Value()
                : Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);

            // Set the root path or default to the current directory
            var rootPath = _rootPath.HasValue()
                ? _rootPath.Value()
                : Directory.GetCurrentDirectory();

            try
            {
                // Ensure the configuration file exists
                if (!File.Exists(configFilePath))
                {
                    System.Console.WriteLine($"Configuration file not found: {configFilePath}");
                    return 1;
                }

                // Generate files from the configuration file
                var generatedFiles = GeneratorSetProcessor.GenerateFilesFromConfigJsonFile(configFilePath, rootPath);

                // Output the generated file paths
                System.Console.WriteLine("Code generation completed. The following files were generated:");
                foreach (var file in generatedFiles)
                {
                    System.Console.WriteLine(file);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"An error occurred during code generation: {ex.Message}, {ex.StackTrace}");
                return 1;
            }

            return 0;
        }
    }
}
