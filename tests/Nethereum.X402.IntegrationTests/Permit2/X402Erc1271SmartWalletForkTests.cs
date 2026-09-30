using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class X402Erc1271SmartWalletForkTests
{
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private static string? ForkRpc => Environment.GetEnvironmentVariable("X402_FORK_RPC");

    [Fact]
    public async Task Eip3009_Verify_AcceptsErc1271ContractWalletSignature()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var deployerKey = EthECKey.GenerateKey();
        var ownerKey = EthECKey.GenerateKey();
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);

        var deployerWeb3 = new Nethereum.Web3.Web3(deployer, rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await deployerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(deployerWeb3, deployer);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");

        var walletDeployment = new Erc1271WalletDeployment { Owner = ownerKey.GetPublicAddress(), FromAddress = deployer.Address, Gas = 2000000 };
        var walletReceipt = await deployerWeb3.Eth.GetContractDeploymentHandler<Erc1271WalletDeployment>()
            .SendRequestAndWaitForReceiptAsync(walletDeployment);
        var wallet = walletReceipt.ContractAddress;

        await usdc.MintAsync(wallet, BigInteger.Parse("10000000"));

        var amount = BigInteger.Parse("1000000");
        var recipient = EthECKey.GenerateKey().GetPublicAddress();
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = Network,
            Amount = amount.ToString(),
            Asset = token,
            PayTo = recipient,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
        };

        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var authorization = builder.BuildFromPaymentRequirements(requirements, wallet);
        var signature = await signer.SignWithPrivateKeyAsync(
            authorization, "USDC", "2", ChainId, token, ownerKey.GetPrivateKey());

        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload { Authorization = authorization, Signature = EncodeSignature(signature) }
        };

        var clients = new Dictionary<int, IClient> { { ChainId, deployerWeb3.Client } };
        var facilitator = new X402TransferWithAuthorisation3009Service(deployerKey.GetPrivateKey(), clients);

        var verify = await facilitator.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.Equal(wallet, verify.Payer);
    }

    [Fact]
    public async Task Eip3009_Verify_RejectsWrongSignerForContractWallet()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var deployerKey = EthECKey.GenerateKey();
        var ownerKey = EthECKey.GenerateKey();
        var strangerKey = EthECKey.GenerateKey();
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);

        var deployerWeb3 = new Nethereum.Web3.Web3(deployer, rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await deployerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(deployerWeb3, deployer);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");

        var walletDeployment = new Erc1271WalletDeployment { Owner = ownerKey.GetPublicAddress(), FromAddress = deployer.Address, Gas = 2000000 };
        var walletReceipt = await deployerWeb3.Eth.GetContractDeploymentHandler<Erc1271WalletDeployment>()
            .SendRequestAndWaitForReceiptAsync(walletDeployment);
        var wallet = walletReceipt.ContractAddress;
        await usdc.MintAsync(wallet, BigInteger.Parse("10000000"));

        var amount = BigInteger.Parse("1000000");
        var recipient = EthECKey.GenerateKey().GetPublicAddress();
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = Network,
            Amount = amount.ToString(),
            Asset = token,
            PayTo = recipient,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { Name = "USDC", Version = "2" }
        };

        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();
        var authorization = builder.BuildFromPaymentRequirements(requirements, wallet);
        var signature = await signer.SignWithPrivateKeyAsync(
            authorization, "USDC", "2", ChainId, token, strangerKey.GetPrivateKey());

        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload { Authorization = authorization, Signature = EncodeSignature(signature) }
        };

        var clients = new Dictionary<int, IClient> { { ChainId, deployerWeb3.Client } };
        var facilitator = new X402TransferWithAuthorisation3009Service(deployerKey.GetPrivateKey(), clients);

        var verify = await facilitator.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidSignature, verify.InvalidReason);
    }

    private static string EncodeSignature(EthECDSASignature signature)
    {
        var bytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(bytes, 0);
        signature.S.CopyTo(bytes, signature.R.Length);
        signature.V.CopyTo(bytes, signature.R.Length + signature.S.Length);
        return bytes.ToHex(true);
    }
}
