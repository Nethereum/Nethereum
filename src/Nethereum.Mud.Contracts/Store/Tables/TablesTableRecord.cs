using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Mud.EncodingDecoding;
using System.Collections.Generic;
using System.Linq;
using static Nethereum.Mud.Contracts.Store.Tables.TablesTableRecord;

namespace Nethereum.Mud.Contracts.Store.Tables
{
    public class TablesTableRecord : TableRecord<TablesKey, TablesValue>
    {
        public TablesTableRecord() : base("store", "Tables")
        {

        }

        public TableSchema GetTableSchema()
        {
            var tableSchema = new TableSchema(Keys.TableId);
            tableSchema.SchemaKeys = Values.GetKeySchemaFields();
            tableSchema.SchemaValues = Values.GetValueSchemaFields();
            return tableSchema;
        }

        public class TablesKey
        {
            [Parameter("bytes32", "tableId", 1)]
            public byte[] TableId { get; set; }

            public Resource GetTableIdResource()
            {
                return ResourceEncoder.Decode(TableId);
            }
        }

        public class TablesValue
        {
            [Parameter("bytes32", "fieldLayout", 1)]
            public byte[] FieldLayout { get; set; }
            [Parameter("bytes32", "keySchema", 2)]
            public byte[] KeySchema { get; set; }
            [Parameter("bytes32", "valueSchema", 3)]
            public byte[] ValueSchema { get; set; }
            [Parameter("bytes", "abiEncodedKeyNames", 4)]
            public byte[] AbiEncodedKeyNames { get; set; }
            [Parameter("bytes", "abiEncodedFieldNames", 5)]
            public byte[] AbiEncodedFieldNames { get; set; }


            public List<string> GetKeyNames()
            {
                return ABIType.CreateABIType("string[]").Decode<List<string>>(AbiEncodedKeyNames.Skip(32).ToArray());
            }

            public List<string> GetValueFieldNames()
            {
                return ABIType.CreateABIType($"string[]").Decode<List<string>>(AbiEncodedFieldNames.Skip(32).ToArray());
            }

            public void SetKeyNames(List<string> keyNames)
            {
                AbiEncodedKeyNames = ABIType.CreateABIType("string[]").Encode(keyNames.ToArray());
            }

            public void SetValueFieldNames(List<string> fieldNames)
            {
                AbiEncodedFieldNames = ABIType.CreateABIType("string[]").Encode(fieldNames.ToArray());
            }

            public void SetValuesFromSchema(SchemaEncoded schemaEncoded)
            {
                FieldLayout = schemaEncoded.FieldLayout;
                KeySchema = schemaEncoded.KeySchema;
                ValueSchema = schemaEncoded.ValueSchema;
                SetKeyNames(schemaEncoded.KeyNames);
                SetValueFieldNames(schemaEncoded.FieldNames);
            }

            public List<FieldInfo> GetValueSchemaFields()
            {
                var fields = SchemaEncoder.Decode(ValueSchema);
                var fieldNames = GetValueFieldNames();
                for (var i = 0; i < fields.Count; i++)
                {
                    fields[i].Name = fieldNames[i];
                }
                return fields;
            }

            public List<FieldInfo> GetKeySchemaFields()
            {
                var fields = SchemaEncoder.Decode(KeySchema, true);
                var keyNames = GetKeyNames();
                for (var i = 0; i < fields.Count; i++)
                {
                    fields[i].Name = keyNames[i];
                }
                return fields;
            }

           
        }
    }
}
