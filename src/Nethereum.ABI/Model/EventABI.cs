using Nethereum.ABI.FunctionEncoding;

namespace Nethereum.ABI.Model
{
    public class EventABI
    {
        private readonly SignatureEncoder signatureEncoder;
        private string sha3Signature;

        public EventABI(string name)
        {
            Name = name;
            signatureEncoder = new SignatureEncoder();
        }

        public string Name { get; }

        public Parameter[] InputParameters { get; set; }

        public string Sha3Signature
        {
            get
            {
                if (sha3Signature != null) return sha3Signature;
                sha3Signature = signatureEncoder.GenerateSha3Signature(Name, InputParameters);
                return sha3Signature;
            }
        }
    }
}