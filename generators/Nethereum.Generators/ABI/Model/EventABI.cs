
namespace Nethereum.Generators.Model
{
    public class EventABI
    {
        public EventABI(string name, ContractABI contract)
        {
            Name = name;
            ContractAbi = contract;
        }

        public string Name { get; }

        public ParameterABI[] InputParameters { get; set; }

        public ContractABI ContractAbi { get; private set; }

    }
}