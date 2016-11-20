using System.Linq;
using System.Text;

namespace Nethereum.ABI.FunctionEncoding
{
    public class SerpentSignatureEncoder : SignatureEncoder
    {
        public override string GenerateSignature(string name, Parameter[] parameters)
        {
            var signature = new StringBuilder();
            signature.Append(name);
            signature.Append(" ");
            signature.Append(string.Join("", parameters.OrderBy(x => x.Order).Select(x => x.SerpentSignature)));
            return signature.ToString();
        }
    }
}