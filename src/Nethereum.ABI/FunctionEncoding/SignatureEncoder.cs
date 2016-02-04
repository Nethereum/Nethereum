using System.Linq;
using System.Text;
using Nethereum.ABI.Util;

namespace Nethereum.ABI.FunctionEncoding
{
    public class SignatureEncoder
    {
        private Sha3Keccack sha3Keccack;

        public SignatureEncoder()
        {
            sha3Keccack = new Sha3Keccack();

        }
        public string GenerateSignature(string name, Parameter[] parameters)
        {
            var signature = new StringBuilder();
            signature.Append(name);
            signature.Append("(");
            var paramNames = string.Join(",", parameters.OrderBy(x => x.Order).Select(x => x.Type));
            signature.Append(paramNames);
            signature.Append(")");
            return signature.ToString();
        }

        

        public string GenerateSha3Signature(string name, Parameter[] parameters)
        {
            var signature = GenerateSignature(name, parameters);
            return sha3Keccack.CalculateHash(signature);
        
        }

        public string GenerateSha3Signature(string name, Parameter[] parameters, int numberOfFirstBytes)
        {
            return GenerateSha3Signature(name,parameters).Substring(0, numberOfFirstBytes*2); 
        }
    }
}