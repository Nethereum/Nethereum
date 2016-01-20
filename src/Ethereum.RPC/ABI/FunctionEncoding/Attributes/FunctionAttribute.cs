using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Ethereum.RPC.ABI.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FunctionAttribute: Attribute
    {
        
        public string Name { get; set; }
        public string Sha3Signature { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class FunctionOutputAttribute : Attribute
    {

    }
}
