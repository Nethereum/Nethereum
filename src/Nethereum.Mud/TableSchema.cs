using Nethereum.Mud.EncodingDecoding;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Mud
{
    public class TableSchema
    {
        public List<FieldInfo> SchemaValues { get; set; } = new List<FieldInfo>();
        public List<FieldInfo> SchemaKeys { get; set; } = new List<FieldInfo>();
        public TableSchema(string nameSpace, string tableName, bool isOffChainTable = false)
        {
            Namespace = nameSpace;
            Name = tableName;
            IsOffChain = isOffChainTable;
        }

        public TableSchema(string name)
        {
            Namespace = String.Empty;
            Name = name;
        }

        public TableSchema(byte[] resourceEncoded)
        {
            var decoded = ResourceEncoder.Decode(resourceEncoded);
            Namespace = decoded.Namespace;
            Name = decoded.Name;
            IsOffChain = decoded.IsOffchainTable();
        }

        public string Namespace { get; protected set; }
        public string Name { get; protected set; }

        public bool IsOffChain { get; protected set; }

        private byte[] _resourceEncoded;
        public byte[] ResourceIdEncoded
        {
            get
            {
                if (_resourceEncoded == null)
                {
                    if (IsOffChain)
                    {
                        _resourceEncoded = ResourceEncoder.EncodeOffchainTable(Namespace, GetTableNameTrimmedForResource());
                    }
                    else
                    {
                        _resourceEncoded = ResourceEncoder.EncodeTable(Namespace, GetTableNameTrimmedForResource());
                    }

                }
                return _resourceEncoded;
            }
        }

        public string GetTableNameTrimmedForResource()
        {
            return ResourceEncoder.TrimNameAsValidSize(Name);
        }


        public List<FieldValue> GetFieldValues(EncodedTableRecord encodedTableRecord)
        {
            var fieldValues = new List<FieldValue>();

            var keyValues = KeyEncoderDecoder.DecodeKeyToFieldValues(encodedTableRecord.Key, SchemaKeys);
            fieldValues.AddRange(keyValues);

            var values = ValueEncoderDecoder.DecodeValuesToFieldValues(encodedTableRecord.EncodedValues, SchemaValues);
            fieldValues.AddRange(values);
            return fieldValues;

        }

    }
}
