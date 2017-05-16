using System.Linq;
using System.Text;
using Nethereum.ABI.Model;
using Nethereum.Util;

namespace Nethereum.ABI.FunctionEncoding
{
    public class SignatureEncoder
    {
        private readonly Sha3Keccack sha3Keccack;

        public SignatureEncoder()
        {
            sha3Keccack = new Sha3Keccack();
        }

        public string GenerateSha3Signature(string name, Parameter[] parameters)
        {
            var signature = GenerateSignature(name, parameters);
            return sha3Keccack.CalculateHash(signature);
        }

        public string GenerateSha3Signature(string name, Parameter[] parameters, int numberOfFirstBytes)
        {
            return GenerateSha3Signature(name, parameters).Substring(0, numberOfFirstBytes*2);
        }

        public virtual string GenerateSignature(string name, Parameter[] parameters)
        {
            var signature = new StringBuilder();
            signature.Append(name);
            signature.Append("(");
            var paramslist = parameters.OrderBy(x => x.Order).Select(x => x.Type).ToArray();
            var paramNames = string.Join(",", paramslist);
            signature.Append(paramNames);
            signature.Append(")");
            return signature.ToString();
        }
    }
}