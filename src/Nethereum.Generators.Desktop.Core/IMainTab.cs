using Eto.Forms;

namespace Nethereum.Generators.Desktop.Core
{
    public interface IMainTab
    {
        TabPage TabPage { get; }
        int Order { get; }
    }
}