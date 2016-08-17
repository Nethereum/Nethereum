namespace Nethereum.ABI.FunctionEncoding
{
    public class EventABI
    {
        private string sha3Signature;

        private readonly SignatureEncoder signatureEncoder;

        public EventABI(string name)
        {
            Name = name;
            signatureEncoder = new SignatureEncoder();
        }

        public string Name { get; }

        public Parameter[] InputParameters { get; set; }

        public string Sha33Signature
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