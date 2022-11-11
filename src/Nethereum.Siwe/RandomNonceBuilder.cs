using System;
using Nethereum.Signer;

namespace Nethereum.Siwe;

public class RandomNonceBuilder
{
    public static string GenerateNewNonce()
    {
        var currentChallenge = DateTime.Now.ToString("O") + "- Challenge";
        //creating a random key, hashing, signing and hashing again to be truly random
        var key = EthECKey.GenerateKey();
        var currentKey = key.GetPrivateKey(); // random key to sign message
        var signer = new MessageSigner();
        return Util.Sha3Keccack.Current.CalculateHash(signer.HashAndSign(currentChallenge, currentKey));
    }
}