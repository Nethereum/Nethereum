using System.Collections.Generic;

namespace Nethereum.Mud.TableRepository
{
    public class TablePredicate
    {
        public string CombineOperator { get; set; } = "AND";
        public List<KeyValueOperator> Conditions { get; set; } = new List<KeyValueOperator>();
        public List<TablePredicate> Groups { get; set; } = new List<TablePredicate>();

        public int? PageSize { get; set; }
        public int? Page { get; set; }
        public string OrderByField { get; set; }
        public bool OrderByDescending { get; set; }
    }

}