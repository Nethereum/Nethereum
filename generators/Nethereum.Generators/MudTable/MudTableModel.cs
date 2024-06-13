using Nethereum.Generators.Core;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.MudTable
{

    public class MudTableModel : TypeMessageModel
    {
        public MudTable MudTable { get; }

        public MudTableModel(MudTable mudTable, string @namespace)
            : base(@namespace, mudTable.Name, "TableRecord")
        {
            MudTable = mudTable;
            InitialiseNamespaceDependencies();
        }

        private void InitialiseNamespaceDependencies()
        {
            NamespaceDependencies.AddRange(new[]
            { "Nethereum.ABI.FunctionEncoding.Attributes",
              "Nethereum.Mud",
              "Nethereum.Mud.Contracts.Core.Tables",
              "Nethereum.Web3",
              "System.Collections.Generic",
              "System.Numerics",
              
            });
        }

        public bool IsSingleton()
        {
            return MudTable.Keys == null || MudTable.Keys.Length == 0;
        }

        public string GetKeyTypeName()
        {
            return $"{Name}Key";
        }

        public string GetValueTypeName()
        {
            return $"{Name}Value";
        }

        public string GetServiceTypeName()
        {
            return $"{Name}TableService";
        }
    }
}