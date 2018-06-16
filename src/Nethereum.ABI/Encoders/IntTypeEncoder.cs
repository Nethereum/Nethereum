using System;
using System.Linq;
using System.Numerics;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.Encoders
{
    public class IntTypeEncoder : ITypeEncoder
    {
        private readonly IntTypeDecoder intTypeDecoder;

        public IntTypeEncoder()
        {
            intTypeDecoder = new IntTypeDecoder();
        }

        public byte[] Encode(object value)
        {
            BigInteger bigInt;

            var stringValue = value as string;

            if (stringValue != null)
                bigInt = intTypeDecoder.Decode<BigInteger>(stringValue);
            else if (value is BigInteger)
                bigInt = (BigInteger) value;
            else if (value.IsNumber())
                bigInt = BigInteger.Parse(value.ToString());
            else
                throw new Exception("Invalid value for type '" + this + "': " + value + " (" + value.GetType() + ")");
            return EncodeInt(bigInt);
        }

        public byte[] EncodeInt(int value)
        {
            return EncodeInt(new BigInteger(value));
        }

        public byte[] EncodeInt(BigInteger value)
        {
            const int maxIntSizeInBytes = 32;
            //It should always be Big Endian.
            var bytes = BitConverter.IsLittleEndian
                            ? value.ToByteArray().Reverse().ToArray()
                            : value.ToByteArray();

            var ret = new byte[maxIntSizeInBytes];

            for (var i = 0; i < ret.Length; i++)
                if (value.Sign < 0)
                    ret[i] = 0xFF;
                else
                    ret[i] = 0;

            Array.Copy(bytes, 0, ret, maxIntSizeInBytes - bytes.Length, bytes.Length);

            return ret;
        }
    }
}