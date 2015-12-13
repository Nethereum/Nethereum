using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        public override byte[] Encode(object value)
        {
            var array = value as IEnumerable;
            if (array != null && !(value is string))
            {
                return EncodeList(array.Cast<object>().ToList());
            }
            else
            {
                throw new Exception("Array value expected for type " + Name);
            }
        }

        public override object Decode(byte[] encoded)
        {

            if (!ElementType.IsDynamic())
            {            
                return DecodeStaticElementType(encoded);
            }
            else
            {
                throw new NotSupportedException("Arrays containing Dynamic Types are not supported");
            }
        }

        protected virtual object DecodeStaticElementType(byte[] encoded)
        {
            var decoded = new List<object>();

            var currentIndex = 0;

            while (currentIndex != encoded.Length)
            {
                var encodedElement = encoded.Skip(currentIndex).Take(ElementType.FixedSize).ToArray();
                decoded.Add(ElementType.Decode(encodedElement));
                var newIndex = currentIndex + ElementType.FixedSize;
                currentIndex = newIndex;
            }

            return decoded;
        }

        public abstract byte[] EncodeList(IList l);
    }
}