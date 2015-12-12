using System;
using System.Numerics;

namespace Ethereum.RPC.ABI
{
    public class BoolType : IntType
    {
        public BoolType() : base("bool")
        {
        }

        public override byte[] Encode(object value)
        {
            if (!(value is bool))
            {
                throw new Exception("Wrong value for bool type: " + value);
            }
            return base.Encode((bool)value ? 1 : 0);
        }

        public override object Decode(byte[] encoded)
        {
            var decodedBitInt = base.Decode(encoded);
            BigInteger unboxed = (BigInteger)decodedBitInt;
            return Convert.ToBoolean((int)unboxed);
        }

        public override object DecodeString(string value)
        {
            return base.DecodeString(value);
        }
    }
}