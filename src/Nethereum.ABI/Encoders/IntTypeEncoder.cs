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
            intTypeDecoder = new IntTypeDecoder();
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
            const int maxIntSizeInBytes = 32;
            var bytes = value.ToByteArray();

            if (value > (BigInteger.Pow(new BigInteger(2), 256) - 1))
            {
                //if value > 2^256 -1 - overflow
                throw new ArgumentOutOfRangeException(nameof(value),
                                                        $"Unsigned integer value must not exceed maximum Solidity value of 115792089237316195423570985008687907853269984665640564039457584007913129639935. Passed value is {value}");
            }
            else if (bytes.Length == 33 && value.Sign == 1 && value <= (BigInteger.Pow(new BigInteger(2), 256) - 1))
            {
                //if 33 bytes, biginteger is positive, and not larger than 2^256 - 1, remove first byte
                bytes = bytes.Take(bytes.Length - 1).ToArray();
            }
            else if (bytes.Length > 32 && value.Sign == -1)
            {
                //beyond 32 bytes - overflow
                throw new ArgumentOutOfRangeException(nameof(value),
                                                        $"Integer value must not exceed minimum Solidity value of -57896044618658097711785492504343953926634992332820282019728792003956564819968. Length of passed value is {value}");
            }

            //It should always be Big Endian.
            if (BitConverter.IsLittleEndian)
                bytes = bytes.Reverse().ToArray();

            if (bytes.Length > maxIntSizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(value),
                                                      $"Integer value must not exceed maximum Solidity size of {maxIntSizeInBytes} bytes. Length of passed value is {bytes.Length}");

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