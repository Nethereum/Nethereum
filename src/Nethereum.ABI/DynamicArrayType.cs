using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;
using System;
using System.Collections;
using System.Linq;

namespace Nethereum.ABI
{
    public class DynamicArrayType : ArrayType
    {
        public DynamicArrayType(string name) : base(name)
        {
            Decoder = new DynamicArrayTypeDecoder(ElementType);
            Encoder = new DynamicArrayTypeEncoder(ElementType);
        }

        public override string CanonicalName => ElementType.CanonicalName + "[]";
        
        public override int FixedSize => -1;
        public override object DecodePackedUsingElementPacked(byte[] encoded, Type type)
        {
            return ((ArrayTypeDecoder)Decoder).DecodePackedUsingElementPacked(encoded, type);
        }

        public override byte[] EncodePackedUsingElementPacked(object value)
        {
            var array = value as IEnumerable;
            if ((array != null) && !(value is string))
                return ((ArrayTypeEncoder)Encoder).EncodeListPackedUsingElementPacked(array.Cast<object>().ToList());
            throw new Exception("Array value expected for type");
           
        }
    }
    
}