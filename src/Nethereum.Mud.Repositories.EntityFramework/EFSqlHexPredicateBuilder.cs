using Nethereum.Mud.TableRepository;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Mud.Repositories.EntityFramework
{
    public class EFSqlHexPredicateBuilder : IEFSqlPredicateBuilder
    {
        public SqlPredicateResult BuildSql(TablePredicate predicate)
        {
            var sqlResult = new StringBuilder();
            var parameters = new Dictionary<string, object>();
            int parameterIndex = 0;

            BuildSqlRecursive(predicate, sqlResult, ref parameterIndex, parameters);

            return new SqlPredicateResult(sqlResult.ToString(), parameters);
        }

        private void BuildSqlRecursive(TablePredicate predicate, StringBuilder sqlResult, ref int parameterIndex, Dictionary<string, object> parameters)
        {
            bool firstCondition = true;

            // Iterate through conditions in the predicate
            foreach (var condition in predicate.Conditions)
            {
                if (!firstCondition)
                {
                    sqlResult.Append($" {condition.UnionOperator} ");  // Use the CombineOperator of the predicate
                }
                else
                {
                    firstCondition = false;
                }

                // Add parameters and build SQL with placeholders
                var tableIdParam = AddParameter(condition.TableId.ToLower(), ref parameterIndex, parameters);
                var addressParam = AddParameter(condition.Address.ToLower(), ref parameterIndex, parameters);
                var keyParam = AddParameter(condition.HexValue.ToLower(), ref parameterIndex, parameters);

                sqlResult.Append($"(tableid = {tableIdParam} AND address = {addressParam} AND {condition.Key} {condition.ComparisonOperator} {keyParam})");
            }

            // Recursively process groups (nested TablePredicate objects)
            foreach (var group in predicate.Groups)
            {
                if (sqlResult.Length > 0)
                {
                    sqlResult.Append($" {group.CombineOperator} ");
                }

                // Recursively build the SQL for the group and wrap in parentheses
                sqlResult.Append("(");
                BuildSqlRecursive(group, sqlResult, ref parameterIndex, parameters);
                sqlResult.Append(")");
            }
        }

        // Add a parameter and return its placeholder (e.g., @p0, @p1)
        private string AddParameter(object value, ref int parameterIndex, Dictionary<string, object> parameters)
        {
            var paramName = $"@p{parameterIndex++}";
            parameters[paramName] = value;
            return paramName;
        }
    }

}