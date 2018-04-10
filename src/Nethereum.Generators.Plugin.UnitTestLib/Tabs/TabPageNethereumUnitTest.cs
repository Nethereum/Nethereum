using Eto.Forms;
using Nethereum.Generators.Desktop.Core.ContractLibrary;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;

namespace Nethereum.Generators.Plugin.UnitTestLib.Tabs
{
    public class TabPageNethereumUnitTest : TabPage
    {
        
        public TabPageNethereumUnitTest()
        {
            var noImplementedCommand = new Command((x, y) => this.ShowInformation("Not implemented yet"));
            var btnGenerateContractLibrary = new Button();
            btnGenerateContractLibrary.Text = "Generate Contract Classes";
           
            btnGenerateContractLibrary.Bind(c => c.Command, this, m => noImplementedCommand);

            var btnGenerateProjectFile = new Button();
            btnGenerateProjectFile.Text = "Generate Project File";
            btnGenerateProjectFile.Bind(c => c.Command, this, m => noImplementedCommand);

            var nethereumLibraryControl = new TableLayout
            {
                Spacing = SpacingPaddingDefaults.Spacing1,
                Padding = SpacingPaddingDefaults.Padding1,
                Rows = {new TableRow(
                        new TableCell(new Label(){Text="Not implemented yet"}, true)
                    ),
                    new TableRow(
                        new TableCell(btnGenerateContractLibrary, true)
                    ),
                    new TableRow(
                        new TableCell(btnGenerateProjectFile, true)
                    ),
                    new TableRow { ScaleHeight = true }
                },

            };

            Content = nethereumLibraryControl;
            Text = "Nethereum Unit Test Library";
        }
    }
}