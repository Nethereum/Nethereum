using System;
using System.Collections;
using System.Linq;
using System.Text;
using Nethereum.ABI.Decoders;
using Nethereum.ABI.Encoders;

namespace Nethereum.ABI
{
    public class StaticArrayType : ArrayType
    {
        internal int Size;

        public StaticArrayType(string name) : base(name)
        {
            IntialiseSize(name);
            Decoder = new ArrayTypeDecoder(ElementType, Size);
            Encoder = new StaticArrayTypeEncoder(ElementType, Size);
        }

        public override string CanonicalName => ElementType.CanonicalName + "[" + Size + "]";

        public override int FixedSize => ElementType.FixedSize * Size;

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

        private void IntialiseSize(string name)
        {
            var indexFirstBracket = name.LastIndexOf("[", StringComparison.Ordinal);
            var indexSecondBracket = name.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);
            var arraySize = name.Substring(indexFirstBracket + 1, indexSecondBracket - (indexFirstBracket + 1));
            Size = int.Parse(arraySize);
        }
    }
}