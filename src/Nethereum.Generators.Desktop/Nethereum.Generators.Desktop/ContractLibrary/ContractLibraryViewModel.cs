using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.Desktop
{
    public class ContractLibraryViewModel: ViewModelBase
    {
        public static CodeGenLanguage[] GetLanguangeOptions()
        {
            return new CodeGenLanguage[] {CodeGenLanguage.CSharp, CodeGenLanguage.Vb, CodeGenLanguage.FSharp};
        }
        private string baseNamespace;

        public string BaseNamespace
        {
            get { return baseNamespace; }
            set
            {
                if (value != baseNamespace)
                {
                    baseNamespace = value;
                    OnPropertyChanged();
                }
            }
        }


        private string projectPath;

        public string ProjectPath
        {
            get { return projectPath; }
            set
            {
                if (value != projectPath)
                {
                    projectPath = value;
                    OnPropertyChanged();
                }
            }
        }

        private string projectName;

        public string ProjectName
        {
            get { return projectName; }
            set
            {
                if (value != projectName)
                {
                    projectName = value;
                    OnPropertyChanged();
                }
            }
        }

        private CodeGenLanguage codeLanguage;

        public CodeGenLanguage CodeLanguage
        {
            get { return codeLanguage; }
            set
            {
                if (value != codeLanguage)
                {
                    codeLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

    }
}
