using System;
using Eto.Forms;
using Eto.Drawing;

namespace Nethereum.Generators.Desktop
{
    public class ContractPanel: Panel
    {
        public ContractPanel()
        {
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
                Spacing = new Size(5, 5), // space between each cell
                Padding = new Padding(10, 10, 10, 10), // space around the table's sides
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
