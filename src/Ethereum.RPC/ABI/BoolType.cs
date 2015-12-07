using System;
using System.Numerics;

namespace Ethereum.ABI.Tests.DNX
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
            var decodedBitInt = (BigInteger)base.Decode(encoded);
            return Convert.ToBoolean(Convert.ToInt32(decodedBitInt.ToString()));
        }

        public override object DecodeString(string value)
        {
            var decodedBitInt = (BigInteger) base.DecodeString(value);
            return Convert.ToBoolean(Convert.ToInt32(decodedBitInt.ToString()));
        }
    }
}