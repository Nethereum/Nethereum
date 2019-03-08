using System;
using System.Linq;

namespace Nethereum.Generators.Core
{
    public abstract class ABITypeToDotNetTypeBase : ITypeConvertor
    {
        public string Convert(string typeName, bool outputArrayAsList = false)
        {
            var indexFirstBracket = typeName.IndexOf("[");
            var numberOfArrays = typeName.Count(x => x == '[');
            if (indexFirstBracket > -1)
            {
                var elementTypeName = typeName.Substring(0, indexFirstBracket);
                if (outputArrayAsList)
                {
                    return GetListType(Convert(elementTypeName, true), numberOfArrays);
                }
                else
                {
                    return GetArrayType(Convert(elementTypeName));
                }
            }
            if ("bool" == typeName)
            {
                return GetBooleanType();
            }
            if (typeName.StartsWith("int"))
            {
                //default
                if (typeName.Length == 3)
                {
                    return GetBigIntegerType();
                }
                var length = Int32.Parse(typeName.Substring(3));

                if (length > 64)
                {
                    return GetBigIntegerType();
                }
                if (length <= 64 && length > 32)
                {
                    return GetLongType();
                }
                //ints are in 8 bits
                if (length == 32)
                {
                    return GetIntType();
                }
                if (length == 16)
                {
                    return GetShortType();
                }
                if (length == 8)
                {
                    return GetSByteType();
                }
            }
            if (typeName.StartsWith("uint"))
            {

                if (typeName.Length == 4)
                {
                    return GetBigIntegerType();
                }
                var length = Int32.Parse(typeName.Substring(4));

                if (length > 64)
                {
                    return GetBigIntegerType();
                }
                if (length <= 64 && length > 32)
                {
                    return GetULongType();
                }
                //uints are in 8 bits steps
                if (length == 32)
                {
                    return GetUIntType();
                }
                if (length == 16)
                {
                    return GetUShortType();
                }
                if (length == 8)
                {
                    return GetByteType();
                }
            }
            if (typeName == "address")
            {
                return GetStringType();
            }
            if (typeName == "string")
            {
                return GetStringType();
            }
            if (typeName == "bytes")
            {
                return GetByteArrayType();
            }
            if (typeName.StartsWith("bytes"))
            {
                return GetByteArrayType();
            }
            return null;
        }

        protected abstract string GetLongType();
        protected abstract string GetULongType();
        protected abstract string GetIntType();
        protected abstract string GetUIntType();
        protected abstract string GetShortType();
        protected abstract string GetUShortType();
        protected abstract string GetByteType();
        protected abstract string GetSByteType();
        protected abstract string GetByteArrayType();
        protected abstract string GetStringType();
        protected abstract string GetBooleanType();
        protected abstract string GetBigIntegerType();
        protected abstract string GetArrayType(string type);
        protected abstract string GetListType(string type, int numberOfArrays = 1);
    }
}