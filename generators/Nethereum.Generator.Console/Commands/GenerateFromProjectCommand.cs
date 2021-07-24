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
            Name = "from-project";
            Description = "Generates Nethereum code based on one or many abi's within a project.";
            _projectPath = Option("-p | --projectPath", "The full project file path or path to the project folder.", CommandOptionType.SingleValue);
            _assemblyName = Option("-a | --assemblyName", "The output assembly name for the project", CommandOptionType.SingleValue);
            OnExecute((Func<int>)RunCommand);
            CodeGenerationWrapper = new CodeGenerationWrapper();

            this.AddHelpOption();
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
