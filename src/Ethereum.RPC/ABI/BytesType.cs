using System;
using Ethereum.RPC.Tests;
using System.Linq;

namespace Ethereum.ABI.Tests.DNX
{

    public class StringType : BytesType
    {
        public StringType() : base("string")
        {
        }

        public override byte[] Encode(object value)
        {
            if (!(value is string))
            {
                throw new Exception("String value expected for type 'string'");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes((string) value);
            //We don't need to check Endian for UTF8
            return base.Encode(bytes, false);
        }

        public override object Decode(byte[] encoded)
        {
            return System.Text.Encoding.UTF8.GetString(encoded, 0, encoded.Length);
        }
    }

    public class BytesType : ABIType
    {
        protected internal BytesType(string name) : base(name)
        {
        }

        public BytesType() : base("bytes")
        {
        }

        public override byte[] Encode(object value)
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

            return ByteUtil.Merge(IntType.EncodeInt(bb.Length), ret);
        }

        public override object Decode(byte[] encoded)
        {
            throw new System.NotSupportedException();
        }

        public override int FixedSize
        {
            get
            {
                return -1;
            }
        }
    }
}