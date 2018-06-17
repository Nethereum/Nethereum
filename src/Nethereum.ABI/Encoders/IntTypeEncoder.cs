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

            if(_signed && value > IntType.MAX_INT256_VALUE) throw new  ArgumentOutOfRangeException(nameof(value),
                $"Signed SmartContract integer must not exceed maximum value for int256: {IntType.MAX_INT256_VALUE.ToString()}. Current value is: {value}");

            if (_signed && value < IntType.MIN_INT256_VALUE) throw new ArgumentOutOfRangeException(nameof(value),
                $"Signed SmartContract integer must not be less than the minimum value for int256: {IntType.MIN_INT256_VALUE.ToString()}. Current value is: {value}");

            if (!_signed && value > IntType.MAX_UINT256_VALUE) throw new ArgumentOutOfRangeException(nameof(value),
                $"Unsigned SmartContract integer must not exceed maximum value for uint256: {IntType.MAX_UINT256_VALUE.ToString()}. Current value is: {value}");

            if (!_signed && value < IntType.MIN_UINT_VALUE) throw new ArgumentOutOfRangeException(nameof(value),
                $"Unsigned SmartContract integer must not be less than the minimum value of uint: {IntType.MIN_UINT_VALUE.ToString()}. Current value is: {value}");

            //It should always be Big Endian.
            var bytes = BitConverter.IsLittleEndian
                            ? value.ToByteArray().Reverse().ToArray()
                            : value.ToByteArray();

            if (bytes.Length == 33 && !_signed)
            {
                if (bytes[0] == 0x00)
                {
                    bytes = bytes.Skip(1).ToArray();
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value),
                        $"Unsigned SmartContract integer must not exceed maximum value for uint256: {IntType.MAX_UINT256_VALUE.ToString()}. Current value is: {value}");
                }
            }

            const int maxIntSizeInBytes = 32;

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