using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.ABI.EIP712
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TypedData<TDomain>: TypedDataRaw
    { 
        
        public TDomain Domain { get; set; }

        public void InitDomainRawValues()
        {
            DomainRawValues = MemberValueFactory.CreateFromMessage(Domain);
        }

        public void SetMessage<T>(T message)
        {
            Message = MemberValueFactory.CreateFromMessage(message);
        }

        public void EnsureDomainRawValuesAreInitialised()
        {
            if (Domain != null || DomainRawValues == null)
            {
                InitDomainRawValues();
            }
        }

        internal TypedDataRaw ToRaw(MemberValue[] message)
        {
            return new TypedDataRaw
            {
                Types = Types,
                PrimaryType = PrimaryType,
                Message = message,
                DomainRawValues = Domain != null || DomainRawValues == null
                    ? MemberValueFactory.CreateFromMessage(Domain)
                    : DomainRawValues
            };
        }
    }
}