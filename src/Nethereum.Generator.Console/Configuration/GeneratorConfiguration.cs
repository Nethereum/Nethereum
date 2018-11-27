using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nethereum.Generators;
using Nethereum.Generators.Core;
using Newtonsoft.Json.Converters;

namespace Nethereum.Generator.Console.Configuration
{
    public class GeneratorConfiguration
    {
        private static readonly JsonConverter _jsonConverter = new StringEnumConverter();

        public CodeGenLanguage Language { get; set; }

        public List<CompiledContract> Contracts { get; set; }

        public string Namespace { get; set; }

        public string OutputFolder { get; set; }

        public IEnumerable<ContractProjectGenerator> GetProjectGenerators()
        {
            foreach (var contract in Contracts)
            {
                yield return new ContractProjectGenerator(
                    contract.SingleAbi,
                    contract.ContractName,
                    contract.Bytecode,
                    Namespace,
                    $"{contract.ContractName}.Service",
                    $"{contract.ContractName}.CQS",
                    $"{contract.ContractName}.DTO",
                    OutputFolder,
                    Path.DirectorySeparatorChar.ToString(),
                    Language
                );
            }
        }

        public void SaveToJson(string outputDirectory, string fileName = null)
        {
            if (fileName == null)
                fileName = GeneratorConfigurationUtils.ConfigFileName;

            var fullPath = Path.Combine(outputDirectory, fileName);

            File.WriteAllText(fullPath, JsonConvert.SerializeObject(this, _jsonConverter), Encoding.UTF8);
        }

        public static GeneratorConfiguration FromJson(string jsonFile, string defaultNamespace)
        {
            var content = File.ReadAllText(jsonFile, Encoding.UTF8);
            var configuration = JsonConvert.DeserializeObject<GeneratorConfiguration>(content, _jsonConverter);
            configuration.Namespace = defaultNamespace;
            return configuration;
        }
    }
}
