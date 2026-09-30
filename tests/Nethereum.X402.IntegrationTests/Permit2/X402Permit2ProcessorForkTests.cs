using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Permit2;

public class X402Permit2ProcessorForkTests
{
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private static string? ForkRpc => Environment.GetEnvironmentVariable("X402_FORK_RPC");

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2 method: facilitator verifies and settles a permit2 payment", Order = 40)]
    public async Task Verify_And_Settle_Permit2_MovesFunds_ThroughWire()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var setup = await SetUpAsync(rpc, approvePermit2: true);
        var amount = BigInteger.Parse("1000000");

        var payload = BuildWirePayload(setup, amount, setup.Recipient);
        var requirements = BuildRequirements(setup.Token, setup.Recipient, amount);

        var wirePayload = RoundTrip(payload);

        var facilitator = new X402ExactPermit2Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);

        var verify = await facilitator.VerifyPaymentAsync(wirePayload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.Equal(setup.Payer.Address, verify.Payer);

        var before = await setup.Usdc.GetBalanceAsync(setup.Recipient);
        var settle = await facilitator.SettlePaymentAsync(wirePayload, requirements);
        Assert.True(settle.Success, settle.ErrorReason);
        var after = await setup.Usdc.GetBalanceAsync(setup.Recipient);
        Assert.Equal(amount, after - before);
    }

    [Fact]
    public async Task Dispatcher_RoutesPermit2_And_Settles()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var setup = await SetUpAsync(rpc, approvePermit2: true);
        var amount = BigInteger.Parse("1000000");
        var wirePayload = RoundTrip(BuildWirePayload(setup, amount, setup.Recipient));
        var requirements = BuildRequirements(setup.Token, setup.Recipient, amount);

        var permit2 = new X402ExactPermit2Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);
        var eip3009 = new X402TransferWithAuthorisation3009Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);
        var dispatcher = new X402ExactSchemeProcessor(eip3009, permit2);

        var before = await setup.Usdc.GetBalanceAsync(setup.Recipient);
        var settle = await dispatcher.SettlePaymentAsync(wirePayload, requirements);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(amount, await setup.Usdc.GetBalanceAsync(setup.Recipient) - before);
    }

    [Fact]
    public async Task Settle_ThenReplaySameNonce_RealPermit2Revert_DecodesToNonceAlreadyUsed()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var setup = await SetUpAsync(rpc, approvePermit2: true);
        var amount = BigInteger.Parse("1000000");
        var typedPayload = BuildWirePayload(setup, amount, setup.Recipient);
        var requirements = BuildRequirements(setup.Token, setup.Recipient, amount);

        var facilitator = new X402ExactPermit2Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);

        var first = await facilitator.SettlePaymentAsync(RoundTrip(typedPayload), requirements);
        Assert.True(first.Success, first.ErrorReason);

        var permit2Payload = (Permit2SchemePayload)typedPayload.Payload;
        var settle = permit2Payload.Permit2Authorization.ToSettleFunction(permit2Payload.Signature);
        var web3 = new Nethereum.Web3.Web3(new Account(setup.DeployerKey.GetPrivateKey(), ChainId), rpc);
        var proxyHandler = web3.Eth.GetContractHandler(X402Permit2Addresses.ExactPermit2Proxy);

        var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
            () => proxyHandler.EstimateGasAsync(settle));

        Assert.Equal("InvalidNonce", Permit2Service.FindCustomError(ex)?.ErrorABI?.Name);
        Assert.Equal(X402ErrorCodes.NonceAlreadyUsed, Permit2SettlementErrorMapper.MapReason(ex));
    }

    [Fact]
    public async Task Verify_RejectsRecipientMismatch()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var setup = await SetUpAsync(rpc, approvePermit2: true);
        var amount = BigInteger.Parse("1000000");
        var wirePayload = RoundTrip(BuildWirePayload(setup, amount, setup.Recipient));
        var otherPayTo = EthECKey.GenerateKey().GetPublicAddress();
        var requirements = BuildRequirements(setup.Token, otherPayTo, amount);

        var facilitator = new X402ExactPermit2Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);
        var verify = await facilitator.VerifyPaymentAsync(wirePayload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Permit2RecipientMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Verify_RejectsAmountMismatch()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var setup = await SetUpAsync(rpc, approvePermit2: true);
        var wirePayload = RoundTrip(BuildWirePayload(setup, BigInteger.Parse("500000"), setup.Recipient));
        var requirements = BuildRequirements(setup.Token, setup.Recipient, BigInteger.Parse("1000000"));

        var facilitator = new X402ExactPermit2Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);
        var verify = await facilitator.VerifyPaymentAsync(wirePayload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Permit2AmountMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task Verify_RejectsMissingPermit2Allowance()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var setup = await SetUpAsync(rpc, approvePermit2: false);
        var amount = BigInteger.Parse("1000000");
        var wirePayload = RoundTrip(BuildWirePayload(setup, amount, setup.Recipient));
        var requirements = BuildRequirements(setup.Token, setup.Recipient, amount);

        var facilitator = new X402ExactPermit2Service(setup.DeployerKey.GetPrivateKey(), setup.Clients);
        var verify = await facilitator.VerifyPaymentAsync(wirePayload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [Fact]
    public async Task Verify_And_Settle_Permit2_FromErc1271SmartWallet_MovesFunds()
    {
        var rpc = ForkRpc;
        if (string.IsNullOrEmpty(rpc)) return;

        var deployerKey = EthECKey.GenerateKey();
        var ownerKey = EthECKey.GenerateKey();
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);
        var owner = new Account(ownerKey.GetPrivateKey(), ChainId);
        var recipient = EthECKey.GenerateKey().GetPublicAddress();

        var deployerWeb3 = new Nethereum.Web3.Web3(deployer, rpc);
        var ownerWeb3 = new Nethereum.Web3.Web3(owner, rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await deployerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);
        await deployerWeb3.Eth.Anvil().SetBalance.SendRequestAsync(owner.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(deployerWeb3, deployer);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");

        var walletDeployment = new Erc1271WalletDeployment { Owner = owner.Address, FromAddress = deployer.Address, Gas = 2000000 };
        var walletReceipt = await deployerWeb3.Eth.GetContractDeploymentHandler<Erc1271WalletDeployment>()
            .SendRequestAndWaitForReceiptAsync(walletDeployment);
        var wallet = walletReceipt.ContractAddress;

        await usdc.MintAsync(wallet, BigInteger.Parse("10000000"));

        var approveData = ownerWeb3.Eth.GetContract(ApproveAbi, token)
            .GetFunction("approve").GetData(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 96));
        var executeReceipt = await ownerWeb3.Eth.GetContract(WalletAbi, wallet).GetFunction("execute")
            .SendTransactionAndWaitForReceiptAsync(owner.Address, new HexBigInteger(1000000), null, null,
                token, approveData.HexToByteArray());
        Assert.Equal(1, (int)executeReceipt.Status.Value);

        var amount = BigInteger.Parse("1000000");
        var clients = new Dictionary<int, IClient> { { ChainId, deployerWeb3.Client } };

        var payload = BuildWalletWirePayload(token, wallet, ownerKey, amount, recipient);
        var wirePayload = RoundTrip(payload);
        var requirements = BuildRequirements(token, recipient, amount);

        var facilitator = new X402ExactPermit2Service(deployerKey.GetPrivateKey(), clients);

        var verify = await facilitator.VerifyPaymentAsync(wirePayload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.Equal(wallet, verify.Payer);

        var before = await usdc.GetBalanceAsync(recipient);
        var settle = await facilitator.SettlePaymentAsync(wirePayload, requirements);
        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(amount, await usdc.GetBalanceAsync(recipient) - before);
    }

    private const string ApproveAbi =
        @"[{""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""amount"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""stateMutability"":""nonpayable"",""type"":""function""}]";

    private const string WalletAbi =
        @"[{""inputs"":[{""name"":""target"",""type"":""address""},{""name"":""data"",""type"":""bytes""}],""name"":""execute"",""outputs"":[{""name"":"""",""type"":""bytes""}],""stateMutability"":""nonpayable"",""type"":""function""}]";

    private static PaymentPayload BuildWalletWirePayload(string token, string walletPayer, EthECKey ownerKey, BigInteger amount, string recipient)
    {
        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = new BigInteger(now + 3600);
        var validAfter = new BigInteger(now - 600);

        var message = new PermitWitnessTransferFrom
        {
            Permitted = new TokenPermissions { Token = token, Amount = amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = nonce,
            Deadline = deadline,
            Witness = new Witness { To = recipient, ValidAfter = validAfter }
        };
        var signature = new Permit2WitnessSigner().Sign(message, ChainId, X402Permit2Addresses.Permit2, ownerKey);

        return new PaymentPayload
        {
            X402Version = 2,
            Accepted = new PaymentRequirements { Scheme = "exact", Network = Network },
            Payload = new Permit2SchemePayload
            {
                Signature = signature,
                Permit2Authorization = new Permit2Authorization
                {
                    From = walletPayer,
                    Permitted = new Permit2TokenPermissions { Token = token, Amount = amount.ToString() },
                    Spender = X402Permit2Addresses.ExactPermit2Proxy,
                    Nonce = nonce.ToString(),
                    Deadline = deadline.ToString(),
                    Witness = new Permit2WitnessData { To = recipient, ValidAfter = validAfter.ToString() }
                }
            }
        };
    }

    private static PaymentRequirements BuildRequirements(string token, string payTo, BigInteger amount) => new()
    {
        Scheme = "exact",
        Network = Network,
        Amount = amount.ToString(),
        Asset = token,
        PayTo = payTo,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2", Name = "USDC", Version = "2" }
    };

    private static PaymentPayload BuildWirePayload(Setup setup, BigInteger amount, string recipient)
    {
        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = new BigInteger(now + 3600);
        var validAfter = new BigInteger(now - 600);

        var message = new PermitWitnessTransferFrom
        {
            Permitted = new TokenPermissions { Token = setup.Token, Amount = amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = nonce,
            Deadline = deadline,
            Witness = new Witness { To = recipient, ValidAfter = validAfter }
        };
        var signature = new Permit2WitnessSigner().Sign(message, ChainId, X402Permit2Addresses.Permit2, setup.PayerKey);

        return new PaymentPayload
        {
            X402Version = 2,
            Accepted = new PaymentRequirements { Scheme = "exact", Network = Network },
            Payload = new Permit2SchemePayload
            {
                Signature = signature,
                Permit2Authorization = new Permit2Authorization
                {
                    From = setup.Payer.Address,
                    Permitted = new Permit2TokenPermissions { Token = setup.Token, Amount = amount.ToString() },
                    Spender = X402Permit2Addresses.ExactPermit2Proxy,
                    Nonce = nonce.ToString(),
                    Deadline = deadline.ToString(),
                    Witness = new Permit2WitnessData { To = recipient, ValidAfter = validAfter.ToString() }
                }
            }
        };
    }

    private static PaymentPayload RoundTrip(PaymentPayload payload) =>
        JsonSerializer.Deserialize<PaymentPayload>(JsonSerializer.Serialize(payload));

    private static async Task<Setup> SetUpAsync(string rpc, bool approvePermit2)
    {
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

        if (approvePermit2)
        {
            var payerWeb3 = new Nethereum.Web3.Web3(payer, rpc);
            await payerWeb3.Eth.ERC20.GetContractService(token)
                .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 96));
        }

        var clients = new Dictionary<int, IClient> { { ChainId, deployerWeb3.Client } };
        return new Setup(deployerKey, payerKey, payer, recipient, token, usdc, clients);
    }

    private sealed record Setup(
        EthECKey DeployerKey, EthECKey PayerKey, Account Payer, string Recipient,
        string Token, USDCDeploymentHelper Usdc, Dictionary<int, IClient> Clients);
}
