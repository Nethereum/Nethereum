using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Mud.TableRepository
{
    public class DynamicTablePredicateBuilder : ITablePredicateBuilder
    {
        private readonly TableSchema _schema;
        private readonly string _address;
        private readonly string _tableId;
        private readonly TablePredicate _predicate = new TablePredicate();

        public DynamicTablePredicateBuilder(TableSchema schema, string address)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _address = address;
            _tableId = schema.ResourceIdEncoded.ToHex(true);
        }

        public DynamicTablePredicateBuilder Where(string fieldName, string op, object value)
        {
            var condition = CreateCondition(fieldName, op, value, "AND");
            _predicate.Conditions.Add(condition);
            return this;
        }

        public DynamicTablePredicateBuilder And(string fieldName, string op, object value)
        {
            var condition = CreateCondition(fieldName, op, value, "AND");
            _predicate.Conditions.Add(condition);
            return this;
        }

        public DynamicTablePredicateBuilder Or(string fieldName, string op, object value)
        {
            var condition = CreateCondition(fieldName, op, value, "OR");
            _predicate.Conditions.Add(condition);
            return this;
        }

        public DynamicTablePredicateBuilder OrderBy(string fieldName, bool descending = false)
        {
            _predicate.OrderByField = fieldName;
            _predicate.OrderByDescending = descending;
            return this;
        }

        public DynamicTablePredicateBuilder Page(int page, int pageSize)
        {
            _predicate.Page = page;
            _predicate.PageSize = pageSize;
            return this;
        }

        public TablePredicate Build()
        {
            return _predicate;
        }

        public TablePredicate Expand()
        {
            return _predicate;
        }

        private KeyValueOperator CreateCondition(string fieldName, string comparisonOperator, object value, string unionOperator)
        {
            var fieldInfo = FindField(fieldName);
            if (fieldInfo == null)
                throw new ArgumentException($"Field '{fieldName}' not found in schema for table '{_schema.Name}'");

            string hexValue = null;
            if (value != null)
            {
                hexValue = ABIType.CreateABIType(fieldInfo.Type).Encode(value).ToHex();
            }

            return new KeyValueOperator
            {
                Key = fieldInfo.IsKey ? $"key{fieldInfo.Order - 1}" : fieldInfo.Name.ToLowerInvariant(),
                PropertyName = fieldInfo.Name,
                AbiType = fieldInfo.Type,
                Name = fieldInfo.Name,
                Order = fieldInfo.Order,
                ComparisonOperator = comparisonOperator,
                HexValue = hexValue,
                Address = _address,
                TableId = _tableId,
                UnionOperator = unionOperator,
                IsValueField = !fieldInfo.IsKey,
                RawValue = value
            };
        }

        private FieldInfo FindField(string fieldName)
        {
            var field = _schema.SchemaKeys
                .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

            if (field != null) return field;

            return _schema.SchemaValues
                .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
