using System.Collections.Generic;

namespace Nethereum.Generators.Model
{
    public class ContractABI
    {
        public FunctionABI[] Functions { get; set; }
        public ConstructorABI Constructor { get; set; }
        public EventABI[] Events { get; set; }
        public StructABI[] Structs { get; set; }

        public List<FunctionABI> GetAllFunctionsWithSameName(string name)
        {
            var allFunctionMatchingName = new List<FunctionABI>();
            foreach (var function in this.Functions)
            {
                if (function.Name == name)
                {
                    allFunctionMatchingName.Add(function);
                }
            }

            return allFunctionMatchingName;
        }

        public List<EventABI> GetAllEventsWithSameName(string name)
        {
            var allEventsMatchingName = new List<EventABI>();
            foreach (var eventAbi in this.Events)
            {
                if (eventAbi.Name == name)
                {
                    allEventsMatchingName.Add(eventAbi);
                }
            }

            return allEventsMatchingName;
        }

    }
}