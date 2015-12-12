using System;
using System.Linq;
using Ethereum.ABI.Tests.DNX;

namespace Ethereum.RPC.ABI
{
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