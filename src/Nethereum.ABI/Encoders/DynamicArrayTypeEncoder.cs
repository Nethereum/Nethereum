using System.Collections;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.Encoders
{
    public class DynamicArrayTypeEncoder : ArrayTypeEncoder
    {
        private readonly ABIType elementType;
        private readonly IntTypeEncoder intTypeEncoder;

        public DynamicArrayTypeEncoder(ABIType elementType)
        {
            this.elementType = elementType;
            intTypeEncoder = new IntTypeEncoder();
        }


        public override byte[] EncodeList(IList l)
        {
            var elems = new byte[l.Count + 1][];
            elems[0] = intTypeEncoder.EncodeInt(l.Count);
            for (var i = 0; i < l.Count; i++)
                elems[i + 1] = elementType.Encode(l[i]);
            return ByteUtil.Merge(elems);
        }
    }
}