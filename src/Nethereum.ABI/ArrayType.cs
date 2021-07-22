using System;

namespace Nethereum.ABI
{
    public abstract class ArrayType : ABIType
    {
        internal ABIType ElementType;

        protected ArrayType(string name) : base(name)
        {
            InitialiseElementType(name);
        }

        public new static ArrayType CreateABIType(string typeName)
        {
            var indexFirstBracket = typeName.IndexOf("[", StringComparison.Ordinal);
            var indexSecondBracket = typeName.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);

            if (indexFirstBracket + 1 == indexSecondBracket)
                return new DynamicArrayType(typeName);
            return new StaticArrayType(typeName);
        }

        private void InitialiseElementType(string name)
        {
            var indexFirstBracket = name.IndexOf("[", StringComparison.Ordinal);
            var elementTypeName = name.Substring(0, indexFirstBracket);
            var indexSecondBracket = name.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);

            var subDim = indexSecondBracket + 1 == name.Length ? "" : name.Substring(indexSecondBracket + 1);
            ElementType = ABIType.CreateABIType(elementTypeName + subDim);
        }
    }
}