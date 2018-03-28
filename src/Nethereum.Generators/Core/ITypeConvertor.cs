namespace Nethereum.Generators.Core
{
    public interface ITypeConvertor
    {
        string ConvertToDotNetType(string typeName, bool outputArrayAsList = false);
    }
}