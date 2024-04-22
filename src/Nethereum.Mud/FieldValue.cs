using System.Linq;
using System.Reflection.Metadata;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI;

namespace Nethereum.Mud
{

    public class FieldValue : FieldInfo
    {
        public object Value { get; set; }

        public FieldValue(ABIType abiType, object value, string name = null, int order = 1) : base(abiType, false, name, order)
        {
            Value = value;
        }

        public FieldValue(ABIType abiType, object value, bool isKey, string name = null, int order = 1) : base(abiType, isKey, name, order)
        {
            Value = value;
        }

        public FieldValue(string type, object value, string name = null, int order = 1) : base(type, false, name, order)
        {
            Value = value;
        }

        public FieldValue(string type, object value, bool isKey, string name = null, int order = 1) : base(type, isKey, name, order)
        {
            Value = value;
        }
    }
    

    public class Schema
    {
        public int TotalLength { get; set; }
        public int NumberDynamicFields { get; set; }
        public int NumberStaticFields { get; set; }
        public byte[] SchemaBytes { get; set; }

        public string[] GetSchemaTypes()
        {
           var totalNumberOfFields =  SchemaBytes[2] + SchemaBytes[3];
           var schemaTypes = new string[totalNumberOfFields];
           for (var i = 0; i < totalNumberOfFields; i++)
           {
               var index = SchemaBytes[i + 4];
               schemaTypes[i] = SchemaEncoder.SchemaAbiTypes[index];
           }
           return schemaTypes;
        }
    }
}
