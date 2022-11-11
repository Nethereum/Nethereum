namespace Nethereum.Generators.Core
{
    public class ABITypeToVBType : ABITypeToDotNetTypeBase
    {
        protected override string GetLongType()
        {
            return "Long";
        }

        protected override string GetULongType()
        {
            return "ULong";
        }

        protected override string GetIntType()
        {
            return "Integer";
        }

        protected override string GetUIntType()
        {
            return "UInteger";
        }

        protected override string GetShortType()
        {
            return "Short";
        }

        protected override string GetUShortType()
        {
            return "UShort";
        }

        protected override string GetByteType()
        {
            return "Byte";
        }

        protected override string GetSByteType()
        {
            return "SByte";
        }

        protected override string GetByteArrayType()
        {
            return "Byte()";
        }

        protected override string GetStringType()
        {
            return "String";
        }

        protected override string GetBooleanType()
        {
            return "Boolean";
        }

        protected override string GetBigIntegerType()
        {
            return "BigInteger";
        }

        protected override string GetArrayType(string type)
        {
            return type + "()";
        }

        protected override string GetListType(string type, int numberOfArrays = 1)
        {
            var output = type;
            for (var i = 0; i < numberOfArrays; i++)
            {
                output = $@"List(Of {output})"; 
            }
            return output;
        }
    }
}