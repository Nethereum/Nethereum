namespace Nethereum.ABI.FunctionEncoding
{
    public class EventABI
    {

        private SignatureEncoder signatureEncoder;

        public EventABI(string name)
        {
            Name = name;
            signatureEncoder = new SignatureEncoder();
        }

        public string Name { get; private set; }

        public EventParameter[] InputParameters { get; set; }
        
        private string sha3Signature;
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