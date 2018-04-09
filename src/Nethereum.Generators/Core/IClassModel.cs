using System.Collections.Generic;

namespace Nethereum.Generators.Core
{
    public interface IClassModel
    {
        string GetTypeName();
        string GetFileName();
        string GetVariableName();
        string Namespace { get; }
        List<string> NamespaceDependencies { get; }
    }
}