using System.Collections.Generic;

namespace Nethereum.Signer.EIP712
{
    public class TypedData<TDomain> where TDomain: IDomain
    {
        public IDictionary<string, MemberDescription[]> Types { get; set; }

        public string PrimaryType { get; set; }

        public TDomain Domain { get; set; }

        public MemberValue[] Message { get; set; }
    }
}