
namespace Nethereum.Generators.Model
{
    public class ErrorABI
    {
        public ErrorABI(string name, ContractABI contract)
        {
            Name = name;
            ContractAbi = contract;
        }

        public ContractABI ContractAbi { get; private set; }
        public string Name { get; }
        public ParameterABI[] InputParameters { get; set; }

    }
}