using System.Collections.Generic;

namespace Nethereum.Generators.Core
{

    public abstract class TypeMessageModel: IClassModel
    {
        protected CommonGenerators CommonGenerators { get; set; }

        public string Namespace { get; }
        public string Name { get; }
        public string ClassNameSuffix { get; protected set; }
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

        public virtual string GetTypeName(string name)
        {
            return $"{CommonGenerators.GenerateClassName(name)}{ClassNameSuffix}";
        }

        public virtual string GetFileName(string name)
        {
            return GetTypeName(name) + ".gen." + CodeGenLanguage.GetCodeOutputFileExtension();
        }

        public string GetVariableName(string name)
        {
            return $"{CommonGenerators.GenerateVariableName(name, CodeGenLanguage)}{ClassNameSuffix}";
        }

        public virtual string GetTypeName()
        {
            return GetTypeName(Name);
        }

        public virtual string GetFileName()
        {
            return GetFileName(Name);
        }

        public string GetVariableName()
        {
            return GetVariableName(Name);
        }
    }
}