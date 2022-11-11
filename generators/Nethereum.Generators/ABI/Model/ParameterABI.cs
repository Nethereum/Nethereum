using System.Collections.Generic;
using Nethereum.Generators.Core;

namespace Nethereum.Generators.Model
{
    public class ParameterABI:Parameter
    {
        public ParameterABI(string type, string name = null, int order = 1, string structType = null):base(name, type, order)
        {
            StructType = structType;
        }

        public ParameterABI(string type, int order) : this(type, null, order)
        {
        }

        public string StructType { get;  set; }
        public bool Indexed { get; set; }
    }
}