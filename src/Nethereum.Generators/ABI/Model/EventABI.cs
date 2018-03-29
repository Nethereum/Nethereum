
namespace Nethereum.Generators.Model
{
    public class EventABI
    {
        public EventABI(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public ParameterABI[] InputParameters { get; set; }

    }
}