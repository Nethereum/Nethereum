using Eto.Forms;
using Nethereum.Generators.Desktop.Core;

namespace Nethereum.Generators.Plugin.UnitTestLib.Tabs
{
    public class TabPageMainNethereumUnitTest : IMainTab
    {
        public TabPageMainNethereumUnitTest(TabPageNethereumUnitTest tabPage)
        {
            TabPage = tabPage;
            Order = 2;
        }

        public TabPage TabPage { get; }
        public int Order { get; }
    }
}