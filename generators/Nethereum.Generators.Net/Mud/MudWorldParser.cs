using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Generators.Model;
using Newtonsoft.Json;
using Nethereum.Generators.MudTable;
using System.Linq;

namespace Nethereum.Generators.Net.Mud
{
    public class MudWorldParser
    {
        public class MudTableExtract
        {
            public string Name { get; set; }
            public Dictionary<string, string> Schema { get; set; }
            public List<string> Key { get; set; } = new List<string>();
        }

        public class MudWorldExtract
        {
            public Dictionary<string, MudTableExtract> Tables { get; set; }
        }
        public List<MudTable.MudTable> ParseJson(string jsonString)
        {
            var world = JsonConvert.DeserializeObject<MudWorldExtract>(jsonString);
            var mudTables = new List<MudTable.MudTable>();
            if (world == null) return mudTables;
            if (world.Tables == null) return mudTables;
            foreach (var table in world.Tables)
            {
                var mudTable = new MudTable.MudTable();
                mudTables.Add(mudTable);
                mudTable.Name = table.Key;
                var valueSchema = new List<ParameterABI>();
                var keys = new List<ParameterABI>();

                var schemaOrder = 0;
                var keyOrder = 0;
                foreach (var schema in table.Value.Schema)
                {

                    if (!table.Value.Key.Any(x => x == schema.Key))
                    {
                        schemaOrder++;
                        valueSchema.Add(new ParameterABI(schema.Value, schema.Key, schemaOrder));
                    }

                }

                mudTable.ValueSchema = valueSchema.ToArray();

                foreach (var key in table.Value.Key)
                {
                    var type = table.Value.Schema[key];
                    keyOrder++;
                    keys.Add(new ParameterABI(type, key, keyOrder));
                }

                mudTable.Keys = keys.ToArray();
            }
            return mudTables;
        }
    }
}
