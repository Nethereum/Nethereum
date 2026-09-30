using System.Threading.Tasks;
using Nethereum.Contracts.Standards.ERC1271;
using Nethereum.Web3;

namespace Nethereum.X402.Blockchain;

/// <summary>
/// Validates a payment signature against an ERC-1271 contract wallet. When the payer address holds
/// contract code, ecrecover cannot recover an EOA, so the facilitator instead asks the wallet
/// whether the signature over the EIP-712 digest is valid (isValidSignature returning the 0x1626ba7e
/// magic value). Used as a fallback by the exact-scheme facilitators after plain ECDSA recovery fails.
/// </summary>
public static class Erc1271SignatureVerifier
{
    /// <summary>
    /// Returns true when <paramref name="signer"/> is a deployed contract that accepts the signature
    /// over <paramref name="digest"/> per ERC-1271. Returns false when the address has no code or the
    /// wallet does not implement / rejects the signature.
    /// </summary>
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
