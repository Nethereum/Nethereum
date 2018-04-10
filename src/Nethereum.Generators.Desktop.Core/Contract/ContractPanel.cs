using Eto.Drawing;
using Eto.Forms;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;

namespace Nethereum.Generators.Desktop.Core.Contract
{
    public class ContractPanel: Panel
    {
        public ContractPanel(ContractViewModel contractViewModel)
        {
            this.DataContext = contractViewModel;

            var abiToolTip = "The ABI of the Ethereum smart contract";
            var txtAbi = new TextBox();
            txtAbi.TextBinding.BindDataContext((ContractViewModel m) => m.Abi);
            txtAbi.ToolTip = abiToolTip;

            var txtByteCode = new TextBox();
            txtByteCode.TextBinding.BindDataContext((ContractViewModel m) => m.ByteCode);

            var txtContractName = new TextBox();
            txtContractName.TextBinding.BindDataContext((ContractViewModel m) => m.ContractName);


            Content = new TableLayout
            {
                Spacing = SpacingPaddingDefaults.Spacing1,
                Padding = SpacingPaddingDefaults.Padding1,
                Rows = {
                    new TableRow(
                        new Label(){Text = "ABI:", ToolTip=abiToolTip},
                        new TableCell(txtAbi, true)
                    ),
                    new TableRow(
                        new Label(){Text = "Byte Code:" },
                        new TableCell(txtByteCode, true)
                    ),
                    new TableRow(
                        new Label(){Text = "Contract Name:" },
                        new TableCell(txtContractName, true)
                    ),

    		        new TableRow { ScaleHeight = true }
                }
            };
         }
    }
}
