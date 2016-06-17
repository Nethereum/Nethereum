using System;
using System.Numerics;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.Encoders
{
    public class Bytes32TypeEncoder : ITypeEncoder
    {
        private IntTypeEncoder intTypeEncoder;

        public Bytes32TypeEncoder()
        {
            this.intTypeEncoder = new IntTypeEncoder();
           
        }

        public byte[] Encode(object value)
        {

            if (value.IsNumber())
            {
                var bigInt = BigInteger.Parse(value.ToString());
                return intTypeEncoder.EncodeInt(bigInt);
            }

            var stringValue = value as string;
            if (stringValue != null)
            {
                var returnBytes = new byte[32];
                var bytes = System.Text.Encoding.UTF8.GetBytes(stringValue);
                Array.Copy(bytes, 0, returnBytes, 0, bytes.Length);
                return returnBytes;
            }

            var bytesValue = value as byte[];
            if (bytesValue != null)
            {
                if (bytesValue.Length > 32) throw new ArgumentException("Expected byte array no bigger than 32 bytes");
                return bytesValue;
            }

            throw new ArgumentException("Expected Numeric Type or String to be Encoded as Bytes32");
        }

    }
}