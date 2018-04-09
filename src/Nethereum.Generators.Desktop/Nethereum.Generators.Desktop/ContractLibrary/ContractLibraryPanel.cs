using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.Desktop
{
    public class ContractLibraryPanel : Panel
    {
        public ContractLibraryPanel()
        {
            Padding = 10;

            var txtBaseNamespace = new TextBox();
            txtBaseNamespace.TextBinding.BindDataContext((ContractLibraryViewModel m) => m.BaseNamespace);

            var txtProjectName = new TextBox();
            txtProjectName.TextBinding.BindDataContext((ContractLibraryViewModel m) => m.ProjectName);

            var txtProjectPath = new TextBox();
            txtProjectPath.TextBinding.BindDataContext((ContractLibraryViewModel m) => m.ProjectPath);

            var cmbLanguage = new ComboBox();
 
            cmbLanguage.ItemKeyBinding = Binding.Property((CodeGenLanguage r)=> r).Convert(r => ((int)r).ToString());
            cmbLanguage.DataStore = ContractLibraryViewModel.GetLanguangeOptions().Cast<Object>();
         
            cmbLanguage.SelectedKeyBinding
                .Convert(r =>
                {
                    if (r == null) return CodeGenLanguage.CSharp;
                    return (CodeGenLanguage) Enum.Parse(typeof(CodeGenLanguage), r);

                }, g => ((int)g).ToString())
                .BindDataContext((ContractLibraryViewModel m) => m.CodeLanguage);

            Content = new TableLayout
            {
                Spacing = new Size(5, 5), // space between each cell
                Padding = new Padding(10, 10, 10, 10), // space around the table's sides
                Rows = {
                    new TableRow(
                        new Label(){Text = "Base Namespace:" },
                        new TableCell(txtBaseNamespace, true)
                    ),
                    new TableRow(
                        new Label(){Text = "Project Path:" },
                        new TableCell(txtProjectPath, true)
                    ),
                    new TableRow(
                        new Label(){Text = "Project Name:" },
                        new TableCell(txtProjectName, true)
                    ),
                    new TableRow(
                        new Label(){Text = "Language:" },
                        new TableCell(cmbLanguage, true)
                    ),

                new TableRow { ScaleHeight = true }
            }
            };
        }
    }
}
