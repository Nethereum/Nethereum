namespace Nethereum.Generators.Core
{
    public abstract class TypeMessageModel: IClassModel
    {
        protected CommonGenerators CommonGenerators { get; set; }

        public string Namespace { get; }

        protected TypeMessageModel(string @namespace)
        {
            Namespace = @namespace;
            CommonGenerators = new CommonGenerators();
        }

        protected abstract string GetClassNameSuffix();
        protected abstract string GetBaseName();

        public string GetTypeName(string name)
        {
            return $"{CommonGenerators.GenerateClassName(name)}{GetClassNameSuffix()}";
        }

        public string GetFileName(string name)
        {
            return GetTypeName(name) + ".cs";
        }

        public string GetVariableName(string name)
        {
            return $"{CommonGenerators.GenerateVariableName(name)}{GetClassNameSuffix()}";
        }

        public string GetTypeName()
        {
            return GetTypeName(GetBaseName());
        }

        public string GetFileName()
        {
            return GetFileName(GetBaseName());
        }

        public string GetVariableName()
        {
            return GetVariableName(GetBaseName());
        }
    }
}