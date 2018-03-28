using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.Model
{
    public class ParameterABI:Parameter
    {
        public ParameterABI(string type, string name = null, int order = 1):base(name, type, order)
        {
            
        }

        public ParameterABI(string type, int order) : this(type, null, order)
        {
        }


        public bool Indexed { get; set; }
    }
}