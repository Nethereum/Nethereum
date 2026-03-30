using Nethereum.DID.Serialization;
using Newtonsoft.Json;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.DID
{
    [Newtonsoft.Json.JsonConverter(typeof(VerificationRelationshipConverter))]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonConverter(typeof(VerificationRelationshipSystemTextJsonConverter))]
#endif
    public class VerificationRelationship
    {
        public string VerificationMethodReference { get; set; }

        public VerificationMethod EmbeddedVerificationMethod { get; set; }

        [Newtonsoft.Json.JsonIgnore]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore]
#endif
        public bool IsReference
        {
            get { return VerificationMethodReference != null; }
        }

        [Newtonsoft.Json.JsonIgnore]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore]
#endif
        public bool IsEmbedded
        {
            get { return EmbeddedVerificationMethod != null; }
        }

        [Newtonsoft.Json.JsonIgnore]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore]
#endif
        public string Id
        {
            get
            {
                if (IsEmbedded)
                    return EmbeddedVerificationMethod.Id;
                return VerificationMethodReference;
            }
        }

        public VerificationRelationship()
        {
        }

        public VerificationRelationship(string reference)
        {
            VerificationMethodReference = reference;
        }

        public VerificationRelationship(VerificationMethod embedded)
        {
            EmbeddedVerificationMethod = embedded;
        }
    }
}
