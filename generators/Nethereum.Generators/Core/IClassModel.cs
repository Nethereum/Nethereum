using System.Collections.Generic;

namespace Nethereum.Generators.Core
{
    public interface IClassModel: IFileModel
    {
        string GetTypeName();
        string GetVariableName();
    }

    public interface IFileModel
    {
        string GetFileName();
        string Namespace { get; }
        List<string> NamespaceDependencies { get; }
    }
}