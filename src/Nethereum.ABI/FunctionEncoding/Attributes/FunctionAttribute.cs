using System;

namespace Nethereum.ABI.FunctionEncoding.Attributes
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
