using System;
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
            Decoder = new ArrayTypeDecoder(ElementType);
            Encoder = new StaticArrayTypeEncoder(ElementType, Size);
        }

        private void IntialiseSize(string name)
        {
            int indexFirstBracket = name.IndexOf("[", StringComparison.Ordinal);
            int indexSecondBracket = name.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);
            string dim = name.Substring(indexFirstBracket + 1, indexSecondBracket - (indexFirstBracket + 1));
            Size = int.Parse(dim);
        }

        public override string CanonicalName => ElementType.CanonicalName + "[" + Size + "]";       

        public override int FixedSize => ElementType.FixedSize * Size;
    }
}