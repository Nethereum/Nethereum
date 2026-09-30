using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Documentation;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class Permit2WitnessSignerTests
{
    private const string PayerKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
    private const int ChainId = 84532;

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2 witness: sign and recover the x402 witness message", Order = 42)]
    public void Sign_RoundTripsToSignerAddress()
    {
        var key = new EthECKey(PayerKey);
        var signer = new Permit2WitnessSigner();

        var message = new PermitWitnessTransferFrom
        {
            Permitted = new TokenPermissions { Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e", Amount = 1000000 },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = BigInteger.Parse("33247007178036348590600198031289925668252061821958005840077069883511451257277"),
            Deadline = 1782911682,
            Witness = new Witness { To = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC", ValidAfter = 1782907482 }
        };

        var signature = signer.Sign(message, ChainId, X402Permit2Addresses.Permit2, key);
        var recovered = signer.RecoverSigner(message, ChainId, X402Permit2Addresses.Permit2, signature);

        Assert.True(recovered.IsTheSameAddress(key.GetPublicAddress()),
            $"recovered {recovered} != signer {key.GetPublicAddress()}");
    }
}
