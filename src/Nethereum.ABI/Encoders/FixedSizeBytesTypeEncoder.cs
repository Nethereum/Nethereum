using System;
using System.Numerics;
using System.Text;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.Encoders
{
    public class FixedSizeBytesTypeEncoder : ITypeEncoder
    {
        private readonly IntTypeEncoder _intTypeEncoder;
        private readonly byte _arraySize;

        public FixedSizeBytesTypeEncoder(byte arraySize)
        {
            _intTypeEncoder = new IntTypeEncoder();
            _arraySize = arraySize;
        }

        public byte[] Encode(object value)
        {
            if (value.IsNumber())
            {
                var bigInt = BigInteger.Parse(value.ToString());
                var encodedIntResult = _intTypeEncoder.EncodeInt(bigInt);
                if (encodedIntResult.Length > _arraySize) {
                    throw new ArgumentException(string.Format("value fills {0} bytes but has {1} allocated", encodedIntResult.Length, _arraySize), nameof(value));
                }
                return encodedIntResult;
            }

            var stringValue = value as string;
            if (stringValue != null)
            {
                var returnBytes = new byte[_arraySize];
                var bytes = Encoding.UTF8.GetBytes(stringValue);

                if (bytes.Length > _arraySize) {
                    throw new ArgumentException(string.Format("value fills {0} bytes but has {1} allocated", bytes.Length, _arraySize), nameof(value));
                }

                Array.Copy(bytes, 0, returnBytes, 0, bytes.Length);
                return returnBytes;
            }

            var bytesValue = value as byte[];
            if (bytesValue != null)
            {
                if (bytesValue.Length > _arraySize) throw new ArgumentException("Expected byte array no bigger than 32 bytes");
                return bytesValue;
            }

            throw new ArgumentException(string.Format("Expected Numeric Type or String to be Encoded as Bytes{0}", _arraySize));
        }
    }
}