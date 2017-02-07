using System.Collections;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.Encoders
{
    public class DynamicArrayTypeEncoder : ArrayTypeEncoder
    {
        private readonly ABIType _elementType;
        private readonly IntTypeEncoder _intTypeEncoder;

        public DynamicArrayTypeEncoder(ABIType elementType)
        {
            this._elementType = elementType;
            _intTypeEncoder = new IntTypeEncoder();
        }

        public override byte[] EncodeList(IList l)
        {
            var elems = new byte[l.Count + 1][];
            elems[0] = _intTypeEncoder.EncodeInt(l.Count);
            for (var i = 0; i < l.Count; i++)
                elems[i + 1] = _elementType.Encode(l[i]);
            return ByteUtil.Merge(elems);
        }
    }
}