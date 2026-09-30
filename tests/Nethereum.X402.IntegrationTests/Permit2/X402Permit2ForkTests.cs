using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class X402Permit2ForkTests
{
    private const int ChainId = 84532;
    private static string? ForkRpc => Environment.GetEnvironmentVariable("X402_FORK_RPC");

    [Fact]
    public async Task PermitWitnessTransfer_SettlesViaProxy_MovesFunds()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc))
            return;

        var deployerKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);
        var payer = new Account(payerKey.GetPrivateKey(), ChainId);
        var recipient = EthECKey.GenerateKey().GetPublicAddress();

        var deployerWeb3 = new Nethereum.Web3.Web3(deployer, rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await deployerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);
        await deployerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(payer.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(deployerWeb3, deployer);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");
        await usdc.MintAsync(payer.Address, BigInteger.Parse("10000000"));

        var payerWeb3 = new Nethereum.Web3.Web3(payer, rpc);
        var payerErc20 = payerWeb3.Eth.ERC20.GetContractService(token);
        await payerErc20.ApproveRequestAndWaitForReceiptAsync(
            X402Permit2Addresses.Permit2, BigInteger.Pow(2, 96));

        var amount = BigInteger.Parse("1000000");
        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = new BigInteger(now + 3600);
        var validAfter = new BigInteger(now - 600);

        var permitted = new TokenPermissions { Token = token, Amount = amount };
        var witness = new Witness { To = recipient, ValidAfter = validAfter };

        var signature = new Permit2WitnessSigner().Sign(
            new PermitWitnessTransferFrom
            {
                Permitted = permitted,
                Spender = X402Permit2Addresses.ExactPermit2Proxy,
                Nonce = nonce,
                Deadline = deadline,
                Witness = witness
            },
            ChainId, X402Permit2Addresses.Permit2, payerKey);

        var recipientBefore = await usdc.GetBalanceAsync(recipient);

        var settle = new SettleFunction
        {
            Permit = new PermitTransferFrom { Permitted = permitted, Nonce = nonce, Deadline = deadline },
            Owner = payer.Address,
            Witness = witness,
            Signature = signature.HexToByteArray()
        };
        var handler = deployerWeb3.Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy);
        var receipt = await handler.SendRequestAndWaitForReceiptAsync(settle);

        Assert.Equal(1, receipt.Status.Value);
        var recipientAfter = await usdc.GetBalanceAsync(recipient);
        Assert.Equal(amount, recipientAfter - recipientBefore);
    }
}
