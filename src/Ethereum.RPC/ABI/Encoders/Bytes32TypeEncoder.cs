using System;
using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
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

            throw new ArgumentException("Expected Numeric Type or String to be Encoded as Bytes32");
        }

    }
}