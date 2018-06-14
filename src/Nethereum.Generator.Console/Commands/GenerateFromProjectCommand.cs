using Microsoft.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.Linq;
using Nethereum.Generator.Console.Generation;

namespace Nethereum.Generator.Console.Commands
{
    public class GenerateFromProjectCommand : CommandLineApplication
    {
        private readonly CommandOption _projectPath;
        private readonly CommandOption _assemblyName;
        public ICodeGenerationWrapper CodeGenerationWrapper { get; set; }

        public GenerateFromProjectCommand()
        {
            Name = "gen-fromproject";
            Description = "Generates a Nethereum (c#) code based based on the abi";
            _projectPath = Option("-p | --projectPath", "The project file name and path", CommandOptionType.SingleValue);
            _assemblyName = Option("-a | --assemblyName", "The output assembly name for the project", CommandOptionType.SingleValue);
            HelpOption("-? | -h | --help");
            OnExecute((Func<int>)RunCommand);
            CodeGenerationWrapper = new CodeGenerationWrapper();
        }

        private int RunCommand()
        {
            var projectPath = _projectPath.Value();
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                projectPath = Environment.CurrentDirectory;
            }

            var assemblyName = _assemblyName.Value();
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = projectPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Last();
            }

            CodeGenerationWrapper.FromProject(projectPath, assemblyName);

            return 0;
        }
    }
}
