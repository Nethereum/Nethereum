using System;
using System.Collections;
using Ethereum.RPC.Util;

namespace Ethereum.RPC.ABI
{
    public class DynamicArrayType : ArrayType
    {
        public DynamicArrayType(string name) : base(name)
        {
        }

        public override string CanonicalName => ElementType.CanonicalName + "[]";

        public override byte[] EncodeList(IList l)
        {
            byte[][] elems = new byte[l.Count + 1][];
            elems[0] = IntType.EncodeInt(l.Count);
            for (int i = 0; i < l.Count; i++)
            {
                elems[i + 1] = ElementType.Encode(l[i]);
            }
            return ByteUtil.Merge(elems);
        }

        public override object Decode(byte[] encoded)
        {
            throw new NotImplementedException();
        }

        public override int FixedSize => -1;
    }
}