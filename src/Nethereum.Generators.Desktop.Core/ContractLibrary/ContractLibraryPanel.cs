using System;
using System.Linq;
using Eto.Forms;
using Nethereum.Generators.Core;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;
using Nethereum.Generators.Desktop.Core.Properties;

namespace Nethereum.Generators.Desktop.Core.ContractLibrary
{
    public class ContractLibraryPanel : Panel
    {
        public ContractLibraryPanel(ContractLibraryViewModel contractLibraryViewModel)
        {
            this.DataContext = contractLibraryViewModel;

            Padding = SpacingPaddingDefaults.Padding1;

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
                Spacing = SpacingPaddingDefaults.Spacing1,
                Padding = SpacingPaddingDefaults.Padding1,
                Rows = {
                    new TableRow(
                        new Label(){Text = Resources.LabelNamespace },
                        new TableCell(txtBaseNamespace, true)
                    ),
                    new TableRow(
                        new Label(){Text = Resources.LabelProjectPath },
                        new TableCell(txtProjectPath, true)
                    ),
                    new TableRow(
                        new Label(){Text = Resources.LabelProjectName },
                        new TableCell(txtProjectName, true)
                    ),
                    new TableRow(
                        new Label(){Text = Resources.LabelCodeLanguage },
                        new TableCell(cmbLanguage, true)
                    ),

                new TableRow { ScaleHeight = true }
            }
            };
        }
    }
}
