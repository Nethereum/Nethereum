using Eto.Forms;

namespace Nethereum.Generators.Desktop.Core
{
    public class TabPageMainNethereumLibrary : IMainTab
    {
        public TabPageMainNethereumLibrary(TabPageNethereumLibrary tabPage)
        {
            TabPage = tabPage;
            Order = 1;
        }

        public TabPage TabPage { get; }
        public int Order { get; }
    }
}