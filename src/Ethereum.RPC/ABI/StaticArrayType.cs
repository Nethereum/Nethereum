using System;
using System.Collections;
using Ethereum.ABI.Tests.DNX;

namespace Ethereum.RPC.ABI
{
    public class StaticArrayType : ArrayType
    {
        internal int Size;

        public StaticArrayType(string name) : base(name)
        {
            IntialiseSize(name);
        }

        private void IntialiseSize(string name)
        {
            int indexFirstBracket = name.IndexOf("[", StringComparison.Ordinal);
            int indexSecondBracket = name.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);
            string dim = name.Substring(indexFirstBracket + 1, indexSecondBracket - (indexFirstBracket + 1));
            Size = int.Parse(dim);
        }

        public override string CanonicalName => ElementType.CanonicalName + "[" + Size + "]";

        public override byte[] EncodeList(IList l)
        {
            if (l.Count != Size)
            {
                throw new Exception("List size (" + l.Count + ") != " + Size + " for type " + Name);
            }

            byte[][] elems = new byte[Size][];
            for (var i = 0; i < l.Count; i++)
            {
                elems[i] = ElementType.Encode(l[i]);
            }
            return ByteUtil.Merge(elems);
        }

        public override object Decode(byte[] encoded)
        {
            throw new NotImplementedException();
        }

        public override int FixedSize => ElementType.FixedSize * Size;
    }
}