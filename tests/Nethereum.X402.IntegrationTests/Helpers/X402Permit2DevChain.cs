using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.DevChain;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;

namespace Nethereum.X402.IntegrationTests.Helpers;

public static class X402Permit2DevChain
{
    public const string Amount = "1000000";

    private static readonly byte[] ReturnsZeroWord = { 0x60, 0x20, 0x60, 0x00, 0xf3 };
    private static readonly byte[] Stop = { 0x00 };

    public static readonly byte[] RevertsOnApprove =
    {
        0x60, 0x00, 0x35, 0x60, 0xe0, 0x1c, 0x63, 0x09, 0x5e, 0xa7, 0xb3, 0x14, 0x60, 0x14, 0x57,
        0x60, 0x20, 0x60, 0x00, 0xf3, 0x5b, 0x60, 0x00, 0x60, 0x00, 0xfd
    };

    public static async Task InstallPermit2StubsAsync(DevChainNode node)
    {
        await node.SetCodeAsync(X402Permit2Addresses.Permit2, ReturnsZeroWord);
        await node.SetCodeAsync(X402Permit2Addresses.ExactPermit2Proxy, Stop);
    }

    public static PaymentRequirements Requirements(int chainId, string token, int maxTimeoutSeconds = 3600) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Caip2.FormatEip155(chainId),
        Amount = Amount,
        Asset = token,
        PayTo = X402DevChainFixture.RecipientAddress,
        MaxTimeoutSeconds = maxTimeoutSeconds,
        Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2, Name = "USDC", Version = "2" }
    };

    public static PaymentPayload Payload(
        EthECKey payerKey,
        int chainId,
        string token,
        Dictionary<string, object>? extensions = null,
        long deadlineSecondsFromNow = 3600,
        int maxTimeoutSeconds = 3600)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var authorization = new Permit2Authorization
        {
            From = payerKey.GetPublicAddress(),
            Permitted = new Permit2TokenPermissions { Token = token, Amount = Amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = new BigInteger(Guid.NewGuid().ToByteArray(), isUnsigned: true).ToString(),
            Deadline = (now + deadlineSecondsFromNow).ToString(),
            Witness = new Permit2WitnessData
            {
                To = X402DevChainFixture.RecipientAddress,
                ValidAfter = (now - 600).ToString()
            }
        };
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(), chainId, X402Permit2Addresses.Permit2, payerKey);

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = Requirements(chainId, token, maxTimeoutSeconds),
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization },
            Extensions = extensions
        };
    }

    public static async Task ApprovePermit2Async(DevChainNode node, EthECKey payerKey, int chainId, string token)
    {
        var web3 = node.CreateWeb3(new Account(payerKey.GetPrivateKey(), chainId));
        await web3.Eth.ERC20.GetContractService(token)
            .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 200));
    }

    public static async Task GiveTokensAsync(DevChainNode node, string holderKey, int chainId, string token, string to, BigInteger amount)
    {
        var web3 = node.CreateWeb3(new Account(holderKey, chainId));
        await web3.Eth.ERC20.GetContractService(token).TransferRequestAndWaitForReceiptAsync(to, amount);
    }

    public static async Task<BigInteger> TransactionCountAsync(DevChainNode node, string address) =>
        (await node.CreateWeb3().Eth.Transactions.GetTransactionCount.SendRequestAsync(address)).Value;
}
