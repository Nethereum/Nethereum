using Nethereum.Generators.Core;
using Nethereum.Generators.MudTable;
using System;
using System.Collections.Generic;

namespace Nethereum.Generators
{

    public class MudTablesGenerator
    {
        public MudTable.MudTable[] MudTables;
        public string Namespace { get; }
        public CodeGenLanguage CodeGenLanguage { get; }
        public string BaseOutputPath { get; }
        public string PathDelimiter { get; }
        public string BaseNamespace { get; }

        public MudTablesGenerator(MudTable.MudTable[] mudTables, string baseNamespace , CodeGenLanguage codeGenLanguage, string baseOutputPath, string pathDelimiter, string @namespace)
        {
            MudTables = mudTables;
            Namespace = @namespace;
            BaseNamespace = baseNamespace;
            CodeGenLanguage = codeGenLanguage;
            BaseOutputPath = baseOutputPath;
            PathDelimiter = pathDelimiter;
        }

        public GeneratedFile[] GenerateAllTables()
        {
            var generators = GetAllTableTypeGenerators();
            var structFullPath = GetFullPath(Namespace);
            var generated = new List<GeneratedFile>();
            foreach (var generator in generators)
            {
                GenerateAndAdd(generated, () => generator.GenerateFileContent(structFullPath));
            }
            return generated.ToArray();
        }

        public List<MudTableGenerator> GetAllTableTypeGenerators()
        {
            var tableNamespace = GetFullNamespace(Namespace);
            var generators = new List<MudTableGenerator>();
            foreach (var table in MudTables)
            {
                var mudTableGenerator = new MudTableGenerator(table, tableNamespace, CodeGenLanguage);
                generators.Add(mudTableGenerator);
            }
            return generators;
        }

        public string GetFullNamespace(string @namespace)
        {
            if (string.IsNullOrEmpty(BaseNamespace)) return @namespace;
            if(string.IsNullOrEmpty(@namespace)) return BaseNamespace;
            return BaseNamespace + "." + @namespace.TrimStart('.');
        }

        public string GetFullPath(string @namespace)
        {
            return BaseOutputPath
                + PathDelimiter
                + @namespace.Replace(".", PathDelimiter);
        }

        private void GenerateAndAdd(List<GeneratedFile> generated, Func<GeneratedFile> generator)
        {
            var generatedFile = generator();
            if (generatedFile != null)
            {
                generated.Add(generatedFile);
            }
        }

    }
}
