using System;

namespace Ethereum.RPC.ABI
{
    public abstract class ArrayType : ABIType
    {
        public static new ArrayType CreateABIType(string typeName)
        {
            int indexFirstBracket = typeName.IndexOf("[", StringComparison.Ordinal);
            int indexSecondBracket = typeName.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);

            if (indexFirstBracket + 1 == indexSecondBracket)
            {
               
                return new DynamicArrayType(typeName);
            }
            else
            {
                return new StaticArrayType(typeName);
            }
        }

        internal ABIType ElementType;

        protected ArrayType(string name) : base(name)
        {
            InitialiseElementType(name);
        }

        private void InitialiseElementType(string name)
        {
            int indexFirstBracket = name.IndexOf("[", StringComparison.Ordinal);
            string elementTypeName = name.Substring(0, indexFirstBracket);
            int indexSecondBracket = name.IndexOf("]", indexFirstBracket, StringComparison.Ordinal);
            string subDim = indexSecondBracket + 1 == name.Length ? "" : name.Substring(indexSecondBracket + 1);
            ElementType = ABIType.CreateABIType(elementTypeName + subDim);
        }

    }
}