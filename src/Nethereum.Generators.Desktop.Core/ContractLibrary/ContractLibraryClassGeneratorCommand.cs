using System;
using Eto.Forms;
using Nethereum.Generators.Desktop.Core.Contract;
using Nethereum.Generators.Desktop.Core.Infrastructure.UI;
using Nethereum.Generators.Net.ContractLibrary;

namespace Nethereum.Generators.Desktop.Core.ContractLibrary
{
    public class ContractLibraryClassGeneratorCommand:Command
    {
        private readonly ContractViewModel _contractViewModel;
        private readonly ContractLibraryViewModel _contractLibraryViewModel;

        public ContractLibraryClassGeneratorCommand(ContractViewModel contractViewModel,
            ContractLibraryViewModel contractLibraryViewModel)
        {
            _contractViewModel = contractViewModel;
            _contractLibraryViewModel = contractLibraryViewModel;
            Executed += GenerateClassesExecuted;
        }

        private void GenerateClassesExecuted(object sender, System.EventArgs e)
        {
            var control = sender as Control;

            try
            {
                var generateClassesCommandCSharp =
                    new GenerateClassesCommand(_contractViewModel.ByteCode,
                        _contractViewModel.Abi,
                        _contractLibraryViewModel.ProjectPath,
                        _contractLibraryViewModel.BaseNamespace,
                        _contractViewModel.ContractName,
                        System.IO.Path.DirectorySeparatorChar.ToString(),
                        _contractLibraryViewModel.CodeLanguage);
                var contractLibraryWriter = new ContractLibraryWriter();
                contractLibraryWriter.WriteClasses(generateClassesCommandCSharp);
                control.ShowInformation("Succefully generated files");
            }
            catch (Exception ex)
            {
                control.ShowError("An error has occurred:" + ex.Message);       
            }

        }
    }
}