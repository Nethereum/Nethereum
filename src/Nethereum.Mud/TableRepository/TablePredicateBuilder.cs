using System.Linq.Expressions;
using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Reflection;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Nethereum.Mud.TableRepository
{

    public class TablePredicateBuilder<TTableRecord, TKey, TValue> : ITablePredicateBuilder, ITablePredicateBuilder<TTableRecord, TKey, TValue>
    where TTableRecord : TableRecord<TKey, TValue>, new()
    where TValue : class, new() where TKey : class, new()
    {
        private TablePredicate _predicate { get; set; } = new TablePredicate();
        public string Address { get; protected set; }
        public byte[] TableResourceIdEncoded { get; protected set; }

        protected string TableId { get; set; }

        private TablePredicate _currentGroup = null;  // Holds all conditions until grouped with another predicate

        public TablePredicateBuilder(string address)
        {
            Address = address;
            TableResourceIdEncoded = ResourceRegistry.GetResource<TTableRecord>().ResourceIdEncoded;
            TableId = TableResourceIdEncoded.ToHex(true);
            _currentGroup = new TablePredicate();  // Initialize the current group to store conditions
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> Equals(Expression<Func<TKey, object>> property, object value)
        {
            AppendCondition(property, value, "=", "AND");
            return this;
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> NotEquals(Expression<Func<TKey, object>> property, object value)
        {
            AppendCondition(property, value, "!=", "AND");
            return this;
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> AndEqual(Expression<Func<TKey, object>> property, object value)
        {
            AppendCondition(property, value, "=", "AND");
            return this;
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> OrEqual(Expression<Func<TKey, object>> property, object value)
        {
            AppendCondition(property, value, "=", "OR");
            return this;
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> AndNotEqual(Expression<Func<TKey, object>> property, object value)
        {
            AppendCondition(property, value, "!=", "AND");
            return this;
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> OrNotEqual(Expression<Func<TKey, object>> property, object value)
        {
            AppendCondition(property, value, "!=", "OR");
            return this;
        }

        private void AppendCondition(Expression<Func<TKey, object>> property, object value, string comparisonOperator, string unionCondition)
        {
            var keyValueOperator = GetKeyPropertyNameAndAbiType(property, value, comparisonOperator);
            keyValueOperator.UnionOperator = unionCondition;  // Set the union operator (AND/OR) for the condition

            // Add to the current group
            _currentGroup.Conditions.Add(keyValueOperator);
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> And(ITablePredicateBuilder other)
        {
            return CombinePredicate(other, "AND");
        }

        public ITablePredicateBuilder<TTableRecord, TKey, TValue> Or(ITablePredicateBuilder other)
        {
            return CombinePredicate(other, "OR");
        }

        private ITablePredicateBuilder<TTableRecord, TKey, TValue> CombinePredicate(ITablePredicateBuilder other, string unionCondition)
        {
            // Finalize current condition group and combine it with the other predicate
            var group = new TablePredicate
            {
                CombineOperator = unionCondition,
                Groups = new List<TablePredicate> { _currentGroup, other.Expand() }
            };

            _currentGroup = group;  // Update current group to this new combination
            return this;
        }

        public TablePredicate Expand()
        {
            return _currentGroup;  // Return the final predicate with all conditions and groups
        }

        private KeyValueOperator GetKeyPropertyNameAndAbiType(Expression<Func<TKey, object>> expression, object value, string comparisonOperator)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                return GetKeyValueOperator(memberExpression.Member, value, comparisonOperator);
            }

            if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression member)
            {
                return GetKeyValueOperator(member.Member, value, comparisonOperator);
            }

            throw new InvalidOperationException("Invalid expression");
        }

        private KeyValueOperator GetKeyValueOperator(MemberInfo member, object value, string comparisonOperator)
        {
            var attribute = member.GetCustomAttribute<ParameterAttribute>();
            if (attribute != null)
            {
                return new KeyValueOperator
                {
                    Key = $"key{attribute.Order - 1}",
                    PropertyName = member.Name,
                    AbiType = attribute.Type,
                    Name = attribute.Name,
                    Order = attribute.Order,
                    ComparisonOperator = comparisonOperator,
                    HexValue = ToHex(value, attribute.Type),
                    Address = Address,
                    TableId = TableId
                };
            }
            throw new InvalidOperationException($"ParameterAttribute attribute is missing for {member.Name}");
        }

        private string ToHex(object value, string type)
        {
            return ABIType.CreateABIType(type).Encode(value).ToHex();
        }
    }

}