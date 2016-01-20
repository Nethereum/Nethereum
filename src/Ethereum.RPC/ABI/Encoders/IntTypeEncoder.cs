using System;
using System.Linq;
using System.Numerics;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    public class IntTypeEncoder : ITypeEncoder
    {
        private IntTypeDecoder intTypeDecoder;

        public IntTypeEncoder()
        {
            this.intTypeDecoder = new IntTypeDecoder();
        }

        public byte[] Encode(object value)
        {
            BigInteger bigInt;

            var stringValue = value as string;

            if (stringValue != null)
            {
                bigInt = intTypeDecoder.Decode<BigInteger>(stringValue);
            }
            else if (value is BigInteger)
            {
                bigInt = (BigInteger)value;
            }
            else if (value.IsNumber())
            {
                bigInt = BigInteger.Parse(value.ToString());
            }
            else
            {
                throw new Exception("Invalid value for type '" + this + "': " + value + " (" + value.GetType() + ")");
            }
            return EncodeInt(bigInt);
        }

        public byte[] EncodeInt(int i)
        {
            return EncodeInt(new BigInteger(i));
        }

        public byte[] EncodeInt(BigInteger bigInt)
        {
            byte[] ret = new byte[32];

            for (int i = 0; i < ret.Length; i++)
            {
                if (bigInt.Sign < 0)
                {
                    ret[i] = 0xFF;
                }
                else
                {
                    ret[i] = 0;
                }
            }

            byte[] bytes;

            //It should always be Big Endian.
            if (BitConverter.IsLittleEndian)
            {
                bytes = bigInt.ToByteArray().Reverse().ToArray();
            }
            else
            {
                bytes = bigInt.ToByteArray().ToArray();
            }

            Array.Copy(bytes, 0, ret, 32 - bytes.Length, bytes.Length);

            return ret;
        }
    }
}