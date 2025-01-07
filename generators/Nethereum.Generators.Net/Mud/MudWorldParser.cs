using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Nethereum.Generators.Model;
using Nethereum.Generators.MudTable;

namespace Nethereum.Generators.Net.Mud
{
    public class Table
    {
        public Dictionary<string, string> Schema { get; set; }
        public List<string> Key { get; set; }
    }

    public class Namespace
    {
        public Dictionary<string, Table> Tables { get; set; }
    }

    public class World
    {
        public string Namespace { get; set; } // Single namespace (old format)
        public Dictionary<string, Table> Tables { get; set; } // Single namespace (old format)
        public Dictionary<string, Namespace> Namespaces { get; set; } // Multiple namespaces (new format)
    }

    public static class MudWorldParser
    {
        public static List<MudTable.MudTable> ExtractTables(string jsonString)
        {
            var world = JsonConvert.DeserializeObject<World>(jsonString);
            var mudTables = new List<MudTable.MudTable>();

            // Check if input format is single namespace or multiple namespaces
            if (world.Tables != null)
            {
                // Single namespace (old format)
                ProcessNamespace(world.Namespace, world.Tables, mudTables);
            }
            else if (world.Namespaces != null)
            {
                // Multiple namespaces (new format)
                foreach (var namespaceEntry in world.Namespaces)
                {
                    ProcessNamespace(namespaceEntry.Key, namespaceEntry.Value.Tables, mudTables);
                }
            }

            return mudTables;
        }

        private static void ProcessNamespace(string namespaceName, Dictionary<string, Table> tables, List<MudTable.MudTable> mudTables)
        {
            foreach (var tableEntry in tables)
            {
                var tableKey = tableEntry.Key;
                var table = tableEntry.Value;

                var mudTable = new MudTable.MudTable();
                mudTables.Add(mudTable);
                mudTable.Name = tableKey;
                mudTable.MudNamespace = namespaceName;

                var schemaOrder = 0;
                var keyOrder = 0;
                var valueParameters = new List<ParameterABI>();
                var keyParameters = new List<ParameterABI>();

                foreach (var schemaEntry in table.Schema)
                {
                    var schemaKey = schemaEntry.Key;
                    var schemaType = schemaEntry.Value;

                    if (!table.Key.Contains(schemaKey))
                    {
                        schemaOrder++;
                        var parameter = new ParameterABI(schemaType, schemaKey, schemaOrder);
                        valueParameters.Add(parameter);
                    }
                }
                mudTable.ValueSchema = valueParameters.ToArray();

                foreach (var key in table.Key)
                {
                    var type = table.Schema[key];
                    keyOrder++;
                    var parameter = new ParameterABI(type, key, keyOrder);
                    keyParameters.Add(parameter);
                }

                mudTable.Keys = keyParameters.ToArray();
            }
        }
    }
}
