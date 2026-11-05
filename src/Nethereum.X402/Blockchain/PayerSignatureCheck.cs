using System;
using System.Threading.Tasks;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.X402.Blockchain;

internal static class PayerSignatureCheck
{
    public const int EcdsaSignatureLength = 65;

    public static async Task<bool> IsValidAsync(
        IWeb3 web3, string payer, byte[] signatureBytes, Func<string?> recoverSigner, Func<byte[]> computeDigest)
    {
        var recovered = signatureBytes.Length == EcdsaSignatureLength ? recoverSigner() : null;
        if (recovered.IsTheSameAddress(payer))
            return true;

        return await Erc1271SignatureVerifier.IsValidContractSignatureAsync(
            web3, payer, computeDigest(), signatureBytes);
    }
}
