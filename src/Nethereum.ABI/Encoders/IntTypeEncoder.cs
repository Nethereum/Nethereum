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
        private readonly bool _signed;

        public IntTypeEncoder(bool signed)
        {
            _signed = signed;
        }

        public IntTypeEncoder() : this(false)
        {

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
            //TODO VALIDATE MAX MIN VALUES waiting for the great pull of @Enigmatic :)
            //int
            //57896044618658097711785492504343953926634992332820282019728792003956564819967
            //-57896044618658097711785492504343953926634992332820282019728792003956564819968
            //uint
            //115792089237316195423570985008687907853269984665640564039457584007913129639935


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