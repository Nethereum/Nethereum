using Eto.Forms;
using Nethereum.Generators.Desktop.Core.ContractLibrary;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;

namespace Nethereum.Generators.Desktop.Core
{
    public class TabPageNethereumLibrary : TabPage
    {

        public TabPageNethereumLibrary(ContractLibraryPanel contractLibraryPanel, 
            ContractLibraryClassGeneratorCommand contractLibraryClassGeneratorCommand,
            NetstandardLibraryGeneratorCommand netstandardLibraryGeneratorCommand
        )
        {
            var btnGenerateContractLibrary = new Button();
            btnGenerateContractLibrary.Text = "Generate Contract Classes";
            btnGenerateContractLibrary.Bind(c => c.Command, this, m => contractLibraryClassGeneratorCommand);

            var btnGenerateProjectFile = new Button();
            btnGenerateProjectFile.Text = "Generate Project File";
            btnGenerateProjectFile.Bind(c => c.Command, this, m => netstandardLibraryGeneratorCommand);

            var nethereumLibraryControl = new TableLayout
            {
                Spacing = SpacingPaddingDefaults.Spacing1,
                Padding = SpacingPaddingDefaults.Padding1,
                Rows = {new TableRow(
                        new TableCell(contractLibraryPanel, true)
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
            Text = "Nethereum library";
        }
    }
}