using Nethereum.ABI.FunctionEncoding;

namespace Nethereum.ABI.Model
{
    public class ErrorABI
    {
        private readonly SignatureEncoder signatureEncoder;

        private string sha3Signature;
        private string signature;

        public ErrorABI(string name)
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
                sha3Signature = signatureEncoder.GenerateSha3Signature(Name, InputParameters, 4);
                return sha3Signature;
            }
        }

        public string Signature
        {

            get
            {
                if (signature != null) return signature;
                signature = signatureEncoder.GenerateSignature(Name, InputParameters);
                return signature;
            }

        }
    }
}
