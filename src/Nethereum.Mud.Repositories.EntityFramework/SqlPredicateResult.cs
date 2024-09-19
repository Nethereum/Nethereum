using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Mud.Repositories.EntityFramework
{
    public class SqlPredicateResult
    {
        public string Sql { get; set; }
        public Dictionary<string, object> Parameters { get; set; }

        public SqlPredicateResult(string sql, Dictionary<string, object> parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }

        public object[] GetParameterValues()
        {
            return Parameters.Values.ToArray();
        }
    }
}