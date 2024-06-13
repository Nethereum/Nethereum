using Nethereum.Generators.Model;
using System.Collections.Generic;

namespace Nethereum.Generators.MudTable
{
    public class MudTable
    {
        public string MudNamespace { get; set; }
        public string Name { get; set; }
        public ParameterABI[] ValueSchema { get; set; }
        public ParameterABI[] Keys { get; set; }
    }
}