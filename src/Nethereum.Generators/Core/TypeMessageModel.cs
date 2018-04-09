using System.Collections.Generic;

namespace Nethereum.Generators.Core
{
    public abstract class TypeMessageModel: IClassModel
    {
        protected CommonGenerators CommonGenerators { get; set; }

        public string Namespace { get; }
        public string Name { get; }
        public string ClassNameSuffix { get; }
        public List<string> NamespaceDependencies { get; }
        public CodeGenLanguage CodeGenLanguage { get; set; }

        protected TypeMessageModel(string @namespace, string name, string classNameSuffix)
        {
            Namespace = @namespace;
            Name = name;
            ClassNameSuffix = classNameSuffix;
            CommonGenerators = new CommonGenerators();
            NamespaceDependencies = new List<string>();
            CodeGenLanguage = CodeGenLanguage.CSharp;
        }

        public string GetTypeName(string name)
        {
            return $"{CommonGenerators.GenerateClassName(name)}{ClassNameSuffix}";
        }

        public string GetFileName(string name)
        {
            return GetTypeName(name) + "." + CodeGenLanguage.GetCodeOutputFileExtension();
        }

        public string GetVariableName(string name)
        {
            return $"{CommonGenerators.GenerateVariableName(name)}{ClassNameSuffix}";
        }

        public string GetTypeName()
        {
            return GetTypeName(Name);
        }

        public string GetFileName()
        {
            return GetFileName(Name);
        }

        public string GetVariableName()
        {
            return GetVariableName(Name);
        }
    }
}