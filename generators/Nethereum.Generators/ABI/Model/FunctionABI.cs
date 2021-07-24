using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.Model
{
    public class FunctionABI: IMessage<ParameterABI>
    {
        public FunctionABI(string name, bool constant, ContractABI contract, bool serpent = false)
        {
            Name = name;
            Serpent = serpent;
            Constant = constant;
            ContractAbi = contract;
        }

        public bool Serpent { get; private set; }

        public bool Constant { get; private set; }

        public string Name { get; set; }

        public ParameterABI[] InputParameters { get; set; }
        public ParameterABI[] OutputParameters { get; set; }

        public ContractABI ContractAbi { get; private set; }

       
    }
}