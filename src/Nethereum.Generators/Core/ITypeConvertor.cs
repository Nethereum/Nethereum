namespace Nethereum.Generators.Core
{
    public interface ITypeConvertor
    {
        string Convert(string typeName, bool outputArrayAsList = false);
    }
}