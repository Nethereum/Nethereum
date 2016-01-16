using System;
using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    public class Bytes32Type: ABIType
    {
        public Bytes32Type(string name): base(name) {
          
        }

        
        public override byte[] Encode(object value) {

            if (value.IsNumber()) {
                BigInteger bigInt = BigInteger.Parse(value.ToString());
                return IntType.EncodeInt(bigInt);
            }

            var stringValue = value as string;
            if (stringValue != null) {
                var returnBytes = new byte[32];
                var bytes = System.Text.Encoding.UTF8.GetBytes(stringValue);
                Array.Copy(bytes, 0, returnBytes, 0, bytes.Length);
                return returnBytes;
            }

            throw new ArgumentException("Expected Numeric Type or String to be Encoded as Bytes32");
        }

        public override object Decode(byte[] encoded) {
            return encoded;
        }

        public static string DecodeString(byte[] encoded)
        {
            return System.Text.Encoding.UTF8.GetString(encoded, 0, encoded.Length);
        }

        public static BigInteger DecodeBigInteger(byte[] encoded)
        {
            return (BigInteger)new IntType("int").Decode(encoded);
        }
    }
}