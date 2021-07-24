using System.Collections.Generic;
using System.IO;
using Nethereum.Generators;
using Nethereum.Generators.Core;

namespace Nethereum.Generator.Console.Models
{
    public class ProjectGenerator
    {
        public string Namespace { get; set; }

        public string OutputFolder { get; set; }

        public CodeGenLanguage Language { get; set; }

        public List<ContractDefinition> Contracts { get; set; }

        public IEnumerable<ContractProjectGenerator> GetProjectGenerators()
        {
            foreach (var contract in Contracts)
            {
                yield return new ContractProjectGenerator(
                    contract.Abi,
                    contract.ContractName,
                    contract.Bytecode,
                    Namespace,
                    $"{contract.ContractName}",
                    $"{contract.ContractName}.ContractDefinition",
                    $"{contract.ContractName}.ContractDefinition",
                    OutputFolder,
                    Path.DirectorySeparatorChar.ToString(),
                    Language
                );
            }
        }
    }
}
