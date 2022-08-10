using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.Signer.EIP712
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TypedData<TDomain>: TypedDataRaw
    { 
        
        public TDomain Domain { get; set; }

        public void InitDomainRawValues()
        {
            DomainRawValues = MemberValueFactory.CreateFromMessage(Domain);
        }

        public void EnsureDomainRawValuesAreInitialised()
        {
           if(DomainRawValues == null)
            {
                InitDomainRawValues();
            }
        }
    }
}