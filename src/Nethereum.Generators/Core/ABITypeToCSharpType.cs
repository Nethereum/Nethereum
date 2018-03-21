using System;

namespace Nethereum.Generators.Core
{
    public class ABITypeToCSharpType
    {
        public string GetTypeMap(string typeName, bool outputMapType = false)
        {
            var indexFirstBracket = typeName.IndexOf("[");
            if (indexFirstBracket > -1)
            {
                var elementTypeName = typeName.Substring(0, indexFirstBracket);
                if (outputMapType)
                {
                    return "List<" + GetTypeMap(elementTypeName, true) + ">";
                }
                else
                {
                    return GetTypeMap(elementTypeName) + "[]";
                }
            }
            if ("bool" == typeName)
            {
                return typeName;
            }
            if (typeName.StartsWith("int"))
            {
                //default
                if (typeName.Length == 3)
                {
                    return "BigInteger";
                }
                var length = Int32.Parse(typeName.Substring(3));

                if (length > 64)
                {
                    return "BigInteger";
                }
                if (length <= 64 && length > 32)
                {
                    return "long";
                }
                //ints are in 8 bits
                if (length == 32)
                {
                    return "int";
                }
                if (length == 16)
                {
                    return "short";
                }
                if (length == 8)
                {
                    return "sbyte";
                }
            }
            if (typeName.StartsWith("uint"))
            {

                if (typeName.Length == 4)
                {
                    return "BigInteger";
                }
                var length = Int32.Parse(typeName.Substring(4));

                if (length > 64)
                {
                    return "BigInteger";
                }
                if (length <= 64 && length > 32)
                {
                    return "ulong";
                }
                //uints are in 8 bits steps
                if (length == 32)
                {
                    return "uint";
                }
                if (length == 16)
                {
                    return "ushort";
                }
                if (length == 8)
                {
                    return "byte";
                }
            }
            if (typeName == "address")
            {
                return "string";
            }
            if (typeName == "string")
            {
                return "string";
            }
            if (typeName == "bytes")
            {
                return "byte[]";
            }
            if (typeName.StartsWith("bytes"))
            {
                return "byte[]";
            }
            return null;
        }
    }
}