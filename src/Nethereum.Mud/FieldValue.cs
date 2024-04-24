using System.Linq;
using System.Reflection.Metadata;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI;
using Nethereum.Mud.EncodingDecoding;

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
    
}
