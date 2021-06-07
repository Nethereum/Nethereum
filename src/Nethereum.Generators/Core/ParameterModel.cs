namespace Nethereum.Generators.Core
{
    public class ParameterModel<TParameter> where TParameter:Parameter
    {
        public CodeGenLanguage CodeGenLanguage { get; private set; }
        public TParameter Parameter { get; set; }
        protected CommonGenerators CommonGenerators { get; }

        public ParameterModel(CodeGenLanguage codeGenLanguage)
        {
            this.CodeGenLanguage = codeGenLanguage;
            CommonGenerators = new CommonGenerators();
        }

        public ParameterModel(TParameter parameter, CodeGenLanguage codeGenLanguage):this(codeGenLanguage)
        {
            Parameter = parameter;
        }

        public virtual string GetVariableName()
        {
            return CommonGenerators.GenerateVariableName(Parameter.Name, CodeGenLanguage);
        }

        public virtual string GetPropertyName()
        {
            return CommonGenerators.GeneratePropertyName(Parameter.Name, CodeGenLanguage);
        }
    }
}