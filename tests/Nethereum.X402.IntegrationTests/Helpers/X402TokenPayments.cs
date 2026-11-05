using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;

namespace Nethereum.X402.IntegrationTests.Helpers;

public static class X402TokenPayments
{
    public static PaymentRequirements Requirements(string token, string amount = "1000000") => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(X402DevChainFixture.ChainId),
        Amount = amount,
        Asset = token,
        PayTo = X402DevChainFixture.RecipientAddress,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { Name = X402DevChainFixture.TokenName, Version = X402DevChainFixture.TokenVersion }
    };

    public static async Task<PaymentPayload> SignedTransferAsync(string token, PaymentRequirements requirements)
    {
        var authorization = new TransferWithAuthorisationBuilder().BuildFromPaymentRequirements(
            requirements, X402DevChainFixture.PayerAddress);
        var signature = await new TransferWithAuthorisationSigner().SignWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, token, X402DevChainFixture.PayerKey);

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = EthECDSASignature.CreateStringSignature(signature)
            }
        };
    }
}
