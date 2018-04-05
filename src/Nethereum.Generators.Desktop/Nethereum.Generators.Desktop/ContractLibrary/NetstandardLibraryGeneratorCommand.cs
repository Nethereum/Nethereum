using Eto.Forms;
using Nethereum.Generators.Net.ContractLibrary;

namespace Nethereum.Generators.Desktop
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
            var contractLibraryWriter = new ContractLibraryWriter();
            contractLibraryWriter.WriteProjectFile(new GenerateProjectFileCommand()
            {
                Path = _contractLibraryViewModel.ProjectPath,
                ProjectName = _contractLibraryViewModel.ProjectName
            });
        }
    }
}