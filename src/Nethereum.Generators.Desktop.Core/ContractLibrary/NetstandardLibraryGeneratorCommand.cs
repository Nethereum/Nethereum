using System;
using Eto.Forms;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;
using Nethereum.Generators.Net.ContractLibrary;

namespace Nethereum.Generators.Desktop.Core.ContractLibrary
{
    public class NetstandardLibraryGeneratorCommand : Command
    {
        private readonly ContractLibraryViewModel _contractLibraryViewModel;

        public NetstandardLibraryGeneratorCommand(
            ContractLibraryViewModel contractLibraryViewModel)
        {
            _contractLibraryViewModel = contractLibraryViewModel;
            Executed += GenerateClassesExecuted;
        }

        private void GenerateClassesExecuted(object sender, System.EventArgs e)
        {
            var control = sender as Control;

            try
            {
                var contractLibraryWriter = new ContractLibraryWriter();
                contractLibraryWriter.WriteProjectFile(new GenerateProjectFileCommand()
                {
                    Path = _contractLibraryViewModel.ProjectPath,
                    ProjectName = _contractLibraryViewModel.ProjectName
                });

            }
            catch (Exception ex)
            {
                control.ShowError("An error has occurred:" + ex.Message);
            }
        }
    }
}