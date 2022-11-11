using System.Collections.Generic;

namespace Nethereum.Generators.Core
{
    public abstract class FileModel : IFileModel
    {
        public string Name { get; }
        protected CommonGenerators CommonGenerators { get; set; }
        public CodeGenLanguage CodeGenLanguage { get; set; }

        public string GetFileName()
        {
            return CommonGenerators.GenerateClassName(Name) + "." + CodeGenLanguage.GetCodeOutputFileExtension();
        }

        public string Namespace { get; }
        public List<string> NamespaceDependencies { get; } = new List<string>();

        public FileModel(string @namespace, string name)
        {
            Namespace = @namespace;
            Name = name;
            CommonGenerators = new CommonGenerators();
            CodeGenLanguage = CodeGenLanguage.CSharp;
        }
    }
}