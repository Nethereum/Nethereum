using System;
using System.Linq;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.Encoders
{
    public class BytesTypeEncoder : ITypeEncoder
    {
        private IntTypeEncoder intTypeEncoder;

        public BytesTypeEncoder()
        {
            this.intTypeEncoder = new IntTypeEncoder();
        }

        public byte[] Encode(object value)
        {
            return Encode(value, true);
        }

        public byte[] Encode(object value, bool checkEndian)
        {
            if (!(value is byte[]))
            {
                throw new Exception("byte[] value expected for type 'bytes'");
            }
            byte[] bb = (byte[])value;
            byte[] ret = new byte[((bb.Length - 1) / 32 + 1) * 32]; // padding 32 bytes

            //It should always be Big Endian.
            if (BitConverter.IsLittleEndian && checkEndian)
            {
                bb = bb.Reverse().ToArray();
            }

            Array.Copy(bb, 0, ret, 0, bb.Length);

            return ByteUtil.Merge(intTypeEncoder.EncodeInt(bb.Length), ret);
        }

    }
}