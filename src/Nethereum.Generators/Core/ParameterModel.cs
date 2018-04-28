namespace Nethereum.Generators.Core
{
    public class ParameterModel<TParameter> where TParameter:Parameter
    {
        public TParameter Parameter { get; set; }
        protected CommonGenerators CommonGenerators { get; }

        public ParameterModel()
        {
            CommonGenerators = new CommonGenerators();
        }

        public ParameterModel(TParameter parameter):this()
        {
            Parameter = parameter;
        }

        public virtual string GetVariableName()
        {
            return CommonGenerators.GenerateVariableName(Parameter.Name);
        }

        public virtual string GetPropertyName()
        {
            return CommonGenerators.GeneratePropertyName(Parameter.Name);
        }
    }
}