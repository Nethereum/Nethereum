using System.Collections.Generic;

namespace Nethereum.Mud.TableRepository
{
    public class TablePredicate
    {
        public string CombineOperator { get; set; } = "AND";  // AND/OR
        public List<KeyValueOperator> Conditions { get; set; } = new List<KeyValueOperator>();
        public List<TablePredicate> Groups { get; set; } = new List<TablePredicate>();  // Nested groups for combined predicates
    }

}