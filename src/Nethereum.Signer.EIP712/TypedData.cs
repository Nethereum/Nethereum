using System.Collections.Generic;

namespace Nethereum.Signer.EIP712
{
    public class TypedData
    {
        public IDictionary<string, MemberDescription[]> Types { get; set; }

        public string PrimaryType { get; set; }

        public Domain Domain { get; set; }

        public MemberValue[] Message { get; set; }
    }
}