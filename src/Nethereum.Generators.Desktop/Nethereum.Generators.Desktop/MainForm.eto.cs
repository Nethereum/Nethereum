using System;
using Eto.Forms;
using Eto.Drawing;
using System.Windows.Input;
using Nethereum.Generators.Desktop.Core.Contract;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Generators.Desktop.Core.ContractLibrary;

namespace Nethereum.Generators.Desktop
{

    partial class MainForm : Form
	{
		void InitializeComponent()
		{
           
            Title = "Nethereum Code Generator";
			ClientSize = new Size(400, 450);
			Padding = SpacingPaddingDefaults.Padding1;


            var contractPanel = Services.GetService<ContractPanel>();

 
            var TabControlMain = new TabControl();
            var tabControlPages = TabControlMain.Pages;
            for(int i=0; i< TabPages.Length; i++)
            {
                tabControlPages.Add(TabPages[i]);
            }
            

            Content = new TableLayout
            {
                Spacing = SpacingPaddingDefaults.Spacing1,
                Padding = SpacingPaddingDefaults.Padding1,
                Rows = {
                    new TableRow(
                        new TableCell(new Label(){Text = "Contract details:" }, true)
                    ),
                    new TableRow(
                        new TableCell(contractPanel, true)
                    ),
                    new TableRow(
                        new TableCell(TabControlMain, true)
                        
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
					
					new ButtonMenuItem { Text = "&File" },
					
				},
				ApplicationItems =
				{
					
				},
				QuitItem = quitCommand,
				AboutItem = aboutCommand
			};

			

            
        }
	}
}
