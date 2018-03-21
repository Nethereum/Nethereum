namespace Nethereum.Generators.Core
{
    public interface IClassModel
    {
        string GetTypeName();
        string GetFileName();
        string GetVariableName();
        string Namespace { get; }
    }
}