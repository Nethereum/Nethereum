namespace Nethereum.Generators.Core
{
    public class ABITypeToCSharpType: ABITypeToDotNetTypeBase
    {
        protected override string GetLongType()
        {
            return "long";
        }

        protected override string GetULongType()
        {
            return "ulong";
        }

        protected override string GetIntType()
        {
            return "int";
        }

        protected override string GetUIntType()
        {
            return "uint";
        }

        protected override string GetShortType()
        {
            return "short";
        }

        protected override string GetUShortType()
        {
            return "ushort";
        }

        protected override string GetByteType()
        {
            return "byte";
        }

        protected override string GetSByteType()
        {
            return "sbyte";
        }

        protected override string GetByteArrayType()
        {
            return "byte[]";
        }

        protected override string GetStringType()
        {
            return "string";
        }

        protected override string GetBooleanType()
        {
            return "bool";
        }

        protected override string GetBigIntegerType()
        {
            return "BigInteger";
        }

        protected override string GetArrayType(string type)
        {
            return type + "[]";
        }

        protected override string GetListType(string type, int numberOfArrays = 1)
        {
            var output = type;
            for (var i = 0; i < numberOfArrays; i++)
            {
                output = "List<" + output + ">";
            }
            return output;
        }
    }
}