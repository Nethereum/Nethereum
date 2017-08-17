using System;
using System.Collections;
using Nethereum.ABI.Util;
using Nethereum.Util;

namespace Nethereum.ABI.Encoders
{
    public class StaticArrayTypeEncoder : ArrayTypeEncoder
    {
        private readonly int arraySize;
        private readonly ABIType elementType;

        public StaticArrayTypeEncoder(ABIType elementType, int arraySize)
        {
            this.elementType = elementType;
            this.arraySize = arraySize;
        }

        public override byte[] EncodeList(IList l)
        {
            if (l.Count != arraySize)
                throw new Exception("List size (" + l.Count + ") != " + arraySize);

            var elems = new byte[arraySize][];
            for (var i = 0; i < l.Count; i++)
                elems[i] = elementType.Encode(l[i]);
            return ByteUtil.Merge(elems);
        }
    }
}