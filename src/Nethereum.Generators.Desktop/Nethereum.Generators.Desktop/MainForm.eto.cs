using System;
using Eto.Forms;
using Eto.Drawing;
using System.Windows.Input;

namespace Nethereum.Generators.Desktop
{

    partial class MainForm : Form
	{
		void InitializeComponent()
		{
           
            Title = "Nethereum Code Generator";
			ClientSize = new Size(400, 350);
			Padding = 10;

            
            var contractPanel = new ContractPanel();
            var contractViewModel = new ContractViewModel();
            contractPanel.DataContext = contractViewModel;
            var nethereumContractLibraryPanel = new ContractLibraryPanel();
            var nethereumContractLibraryViewModel = new ContractLibraryViewModel();
            nethereumContractLibraryPanel.DataContext = nethereumContractLibraryViewModel;

            var nethereumContractLibraryCommand = new ContractLibraryClassGeneratorCommand(contractViewModel, nethereumContractLibraryViewModel);

            var btnGenerateContractLibrary = new Button();
            btnGenerateContractLibrary.Text = "Generate Contract Classes";
            btnGenerateContractLibrary.Bind(c => c.Command, this, m => nethereumContractLibraryCommand);

            var generateNetstandardCommand = new NetstandardLibraryGeneratorCommand(nethereumContractLibraryViewModel);

            var btnGenerateProjectFile = new Button();
            btnGenerateProjectFile.Text = "Generate Project File";
            btnGenerateProjectFile.Bind(c => c.Command, this, m => generateNetstandardCommand);

            

            Content = new TableLayout
            {
                Spacing = new Size(5, 5), // space between each cell
                Padding = new Padding(10, 10, 10, 10), // space around the table's sides
                Rows = {
                    new TableRow(
                        new TableCell(new Label(){Text = "Contract details:" }, true)
                    ),
                    new TableRow(
                        new TableCell(contractPanel, true)
                    ),
                    new TableRow(
                        new Label(){Text = "Nethereum library details" }
                        
                    ),
                    new TableRow(
                       new TableCell(nethereumContractLibraryPanel, true)
                    ),
                     new TableRow(
                       new TableCell(btnGenerateContractLibrary, true)
                    ),
                    new TableRow(
                       new TableCell(btnGenerateProjectFile, true)
                    ),

		// by default, the last row & column will get scaled. This adds a row at the end to take the extra space of the form.
		// otherwise, the above row will get scaled and stretch the TextBox/ComboBox/CheckBox to fill the remaining height.
		        new TableRow { ScaleHeight = true }
    }
            };

			var quitCommand = new Command { MenuText = "Quit", Shortcut = Application.Instance.CommonModifier | Keys.Q };
			quitCommand.Executed += (sender, e) => Application.Instance.Quit();

            
			var aboutCommand = new Command { MenuText = "About..." };
			aboutCommand.Executed += (sender, e) => new AboutDialog().ShowDialog(this);

			// create menu
			Menu = new MenuBar
			{
				Items =
				{
					// File submenu
					new ButtonMenuItem { Text = "&File" },
					// new ButtonMenuItem { Text = "&Edit", Items = { /* commands/items */ } },
					// new ButtonMenuItem { Text = "&View", Items = { /* commands/items */ } },
				},
				ApplicationItems =
				{
					// application (OS X) or file menu (others)
					//new ButtonMenuItem { Text = "&Preferences..." },
				},
				QuitItem = quitCommand,
				AboutItem = aboutCommand
			};

			// create toolbar			
			//ToolBar = new ToolBar { Items = { clickMe } };

            var model = new ContractLibraryViewModel();
            DataContext = model;
        }
	}
}
