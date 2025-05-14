using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Nethereum.Generators.Core
{
    public enum CodeGenLanguage
    {
        CSharp,
        Vb,
        Proto,
        FSharp,
        Razor
    }

    public enum SharedDTOType
    {
        Functions,
        Events,
        Structs
    }
}
