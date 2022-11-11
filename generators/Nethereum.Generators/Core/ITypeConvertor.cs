namespace Nethereum.Generators.Core
{
    public interface ITypeConvertor
    {
        string Convert(string typeName, string dotnetClassName, bool outputArrayAsList = false);
    }
}