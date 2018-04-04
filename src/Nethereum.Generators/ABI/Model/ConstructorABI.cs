namespace Nethereum.Generators.Model
{
    public class ConstructorABI
    {
        public ConstructorABI()
        {
            InputParameters = new ParameterABI[0];
        }
        public ParameterABI[] InputParameters { get; set; }
    }
}