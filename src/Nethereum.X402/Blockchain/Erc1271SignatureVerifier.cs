using Nethereum.Documentation;
using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ERC1271;
using Nethereum.Web3;

namespace Nethereum.X402.Blockchain;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Erc1271SignatureVerifier - contract wallet signature check")]
public static class Erc1271SignatureVerifier
{
    public static async Task<bool> IsValidContractSignatureAsync(IWeb3 web3, string signer, byte[] digest, byte[] signature)
    {
        var code = await web3.Eth.GetCode.SendRequestAsync(signer).ConfigureAwait(false);
        if (string.IsNullOrEmpty(code) || code == "0x")
            return false;

        try
        {
            return await new ERC1271Service(web3.Eth).GetContractService(signer)
                .IsValidSignatureAndValidateReturnQueryAsync(digest, signature)
                .ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }
}
