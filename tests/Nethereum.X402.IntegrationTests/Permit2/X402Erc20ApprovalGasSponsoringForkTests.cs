using System.Numerics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Client;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Xunit;
using Nethereum.Testing;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Trait("Category", "LiveNetwork")]
public class X402Erc20ApprovalGasSponsoringForkTests
{
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private const string Usdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const int UsdcBalanceSlot = 9;
    private const string Key = X402ExtensionKeys.Erc20ApprovalGasSponsoring;
    private static readonly BigInteger Amount = 1_000_000;
    private static readonly BigInteger GenerousGasCostCap = BigInteger.Parse("1000000000000000");
    private static readonly BigInteger OneEther = BigInteger.Parse("1000000000000000000");

    private static string? ForkRpc => TestEnvironment.ServiceOrNull("X402ForkRpc");

    [SkippableFact]
    public async Task Given_NoAllowance_And_Erc20ApprovalSponsoringEnabled_When_Settling_Then_ApprovalIsRelayedAndTheAmountMoves()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc, GenerousGasCostCap);
        Assert.Equal(BigInteger.Zero, await setup.PayerAllowanceAsync());
        Assert.Equal(BigInteger.Zero, await setup.EthBalanceOfAsync(setup.Payer.Address));
        var facilitatorNonceBefore = await setup.TransactionCountAsync(setup.FacilitatorAddress);
        using var server = CreateResourceServer(setup);
        var client = new X402HttpClient(server.CreateClient(), setup.PayerKey.GetPrivateKey(), new X402HttpClientOptions
        {
            PreferredNetwork = Network,
            PreferredScheme = "exact",
            MaxAmount = "10000000",
            RpcClientsByChainId = new Dictionary<int, IClient> { [ChainId] = new Nethereum.JsonRpc.Client.RpcClient(new Uri(rpc)) }
        });

        var response = await client.GetAsync("/premium");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Amount, await setup.BalanceOfAsync(setup.Recipient));
        Assert.True(await setup.PayerAllowanceAsync() > BigInteger.Zero);
        Assert.Equal(BigInteger.One, await setup.TransactionCountAsync(setup.Payer.Address));
        Assert.Equal(facilitatorNonceBefore + 2, await setup.TransactionCountAsync(setup.FacilitatorAddress));
    }

    [SkippableFact]
    public async Task Given_PayerAlreadyHasGasFunds_When_Settling_Then_NoFundingTransactionIsSent()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc, GenerousGasCostCap);
        await setup.Web3.Eth.Anvil().SetBalance.SendRequestAsync(setup.Payer.Address, new HexBigInteger(OneEther));
        var facilitatorNonceBefore = await setup.TransactionCountAsync(setup.FacilitatorAddress);
        var payload = await SignedPayloadAsync(setup, await setup.ApprovalSpecAsync());

        var settle = await setup.Facilitator.SettlePaymentAsync(payload, setup.Requirements);

        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(Amount, await setup.BalanceOfAsync(setup.Recipient));
        Assert.Equal(facilitatorNonceBefore + 1, await setup.TransactionCountAsync(setup.FacilitatorAddress));
        Assert.True(await setup.EthBalanceOfAsync(setup.Payer.Address) < OneEther);
    }

    [SkippableFact]
    public async Task Given_ApprovalGasCostOverTheCap_When_Verifying_Then_Erc20ApprovalTxGasCostExceedsCap()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc, gasCostCap: 1);
        var payload = await SignedPayloadAsync(setup, await setup.ApprovalSpecAsync());

        var verify = await setup.Facilitator.VerifyPaymentAsync(payload, setup.Requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxGasCostExceedsCap, verify.InvalidReason);
        Assert.Equal(BigInteger.Zero, await setup.EthBalanceOfAsync(setup.Payer.Address));
    }

    [SkippableFact]
    public async Task Given_ApprovalWithAStaleNonce_When_Verifying_Then_Erc20ApprovalTxNonceMismatch()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc, GenerousGasCostCap);
        var spec = (await setup.ApprovalSpecAsync()) with { Nonce = 5 };
        var payload = await SignedPayloadAsync(setup, spec);

        var verify = await setup.Facilitator.VerifyPaymentAsync(payload, setup.Requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxNonceMismatch, verify.InvalidReason);
    }

    [SkippableFact]
    public async Task Given_ApprovalWithAMaxFeeBelowTheBaseFee_When_Verifying_Then_Erc20ApprovalTxMaxFeeBelowBaseFee()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc, GenerousGasCostCap);
        var baseFee = (await setup.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
            .SendRequestAsync(BlockParameter.CreateLatest())).BaseFeePerGas.Value;
        Skip.If(baseFee <= 1, "The fork reports a base fee of at most 1 wei, so a lower max fee cannot be signed.");
        var spec = (await setup.ApprovalSpecAsync()) with { MaxFeePerGas = 1 };
        var payload = await SignedPayloadAsync(setup, spec);

        var verify = await setup.Facilitator.VerifyPaymentAsync(payload, setup.Requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Erc20ApprovalTxMaxFeeBelowBaseFee, verify.InvalidReason);
    }

    [SkippableFact]
    public async Task Given_ApprovalMined_When_ProxySettleFails_Then_TheSettleFailureIsReturnedAndTheApprovalStands()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc, GenerousGasCostCap);
        var spec = await setup.ApprovalSpecAsync();
        var signed = Erc20ApprovalTransactions.Sign(setup.PayerKey, spec);
        var gasCost = spec.EffectiveGas * spec.MaxFeePerGas;
        Assert.Equal(BigInteger.Zero, await setup.EthBalanceOfAsync(setup.Payer.Address));
        var delegateCalls = 0;
        var request = new Erc20ApprovalSponsoredSettlementRequest(
            setup.Web3,
            Network,
            setup.Payer.Address,
            signed,
            gasCost,
            _ =>
            {
                delegateCalls++;
                return Task.FromResult(new SettlementResponse
                {
                    Success = false,
                    ErrorReason = X402ErrorCodes.Permit2InvalidSignature,
                    Network = Network,
                    Payer = setup.Payer.Address,
                    Transaction = ""
                });
            });

        var response = await new Erc20ApprovalSponsoredSettlement().SettleAsync(request);

        Assert.Equal(1, delegateCalls);
        Assert.False(response.Success);
        Assert.Equal(X402ErrorCodes.Permit2InvalidSignature, response.ErrorReason);
        Assert.Equal(SignedApprovalSpec.MaxUint256, await setup.PayerAllowanceAsync());
        var approvalHash = "0x" + Nethereum.Model.TransactionFactory.CreateTransaction(signed).Hash.ToHex();
        var receipt = await setup.Web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(approvalHash);
        Assert.Equal(BigInteger.One, receipt.Status.Value);
        var spent = receipt.GasUsed.Value * receipt.EffectiveGasPrice.Value;
        Assert.Equal(gasCost - spent, await setup.EthBalanceOfAsync(setup.Payer.Address));
    }

    private static TestServer CreateResourceServer(Setup setup)
    {
        var processor = new X402ExactSchemeProcessor(
            new X402TransferWithAuthorisation3009Service(setup.FacilitatorKey.GetPrivateKey(), setup.Clients),
            setup.Facilitator);
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(new ProcessorFacilitatorClient(processor)))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(new RoutePaymentConfig("/premium", setup.Requirements)
                    {
                        Extensions = new Dictionary<string, object> { [Key] = X402GasSponsoringExtensions.DeclareErc20Approval() }
                    });
                });
                app.Run(context => context.Response.WriteAsync("premium content"));
            });
        return new TestServer(builder);
    }

    private static Task<PaymentPayload> SignedPayloadAsync(Setup setup, SignedApprovalSpec spec)
    {
        var nonceBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonceBytes);
        var permit2Nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var authorization = new Permit2Authorization
        {
            From = setup.Payer.Address,
            Permitted = new Permit2TokenPermissions { Token = Usdc, Amount = Amount.ToString() },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = permit2Nonce.ToString(),
            Deadline = new BigInteger(now + 3600).ToString(),
            Witness = new Permit2WitnessData { To = setup.Recipient, ValidAfter = new BigInteger(now - 600).ToString() }
        };
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(), ChainId, X402Permit2Addresses.Permit2, setup.PayerKey);

        var info = Erc20ApprovalTransactions.Info(
            setup.Payer.Address, spec, Erc20ApprovalTransactions.Sign(setup.PayerKey, spec));

        return Task.FromResult(new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = setup.Requirements,
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization },
            Extensions = Erc20ApprovalTransactions.Extension(info)
        });
    }

    private static async Task<Setup> SetUpAsync(string rpc, BigInteger gasCostCap)
    {
        var facilitatorKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var recipient = EthECKey.GenerateKey().GetPublicAddress();
        var facilitatorAccount = new Account(facilitatorKey.GetPrivateKey(), ChainId);
        var payer = new Account(payerKey.GetPrivateKey(), ChainId);

        var web3 = new Nethereum.Web3.Web3(facilitatorAccount, rpc);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(
            facilitatorAccount.Address, new HexBigInteger(BigInteger.Parse("100000000000000000000")));

        var slot = new HexBigInteger(
            new BigInteger(Sha3Keccack.Current.CalculateHash(
                payer.Address.HexToByteArray().PadTo32Bytes()
                    .Concat(new BigInteger(UsdcBalanceSlot).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes())
                    .ToArray()),
                isUnsigned: true, isBigEndian: true));
        await web3.Eth.Anvil().SetStorageAt.SendRequestAsync(
            Usdc, slot, (Amount * 10).ToByteArray(isUnsigned: true, isBigEndian: true));

        var clients = new Dictionary<int, IClient> { [ChainId] = web3.Client };
        var requirements = new PaymentRequirements
        {
            Scheme = X402Schemes.Exact,
            Network = Network,
            Amount = Amount.ToString(),
            Asset = Usdc,
            PayTo = recipient,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2, Name = "USDC", Version = "2" }
        };

        var facilitator = new X402ExactPermit2Service(
            facilitatorKey.GetPrivateKey(),
            clients,
            new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = gasCostCap });
        var setup = new Setup(web3, facilitatorKey, facilitatorAccount.Address, payerKey, payer, recipient, clients, requirements, facilitator);
        Assert.Equal(Amount * 10, await setup.BalanceOfAsync(payer.Address));
        return setup;
    }

    private sealed class ProcessorFacilitatorClient : IFacilitatorClient
    {
        private readonly IX402PaymentProcessor _processor;

        public ProcessorFacilitatorClient(IX402PaymentProcessor processor) => _processor = processor;

        public Task<VerificationResponse> VerifyAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            _processor.VerifyPaymentAsync(paymentPayload, requirements, cancellationToken);

        public Task<SettlementResponse> SettleAsync(PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            _processor.SettlePaymentAsync(paymentPayload, requirements, cancellationToken);

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            _processor.GetSupportedAsync(cancellationToken);
    }

    private sealed record Setup(
        Nethereum.Web3.Web3 Web3,
        EthECKey FacilitatorKey,
        string FacilitatorAddress,
        EthECKey PayerKey,
        Account Payer,
        string Recipient,
        Dictionary<int, IClient> Clients,
        PaymentRequirements Requirements,
        X402ExactPermit2Service Facilitator)
    {
        public Task<BigInteger> BalanceOfAsync(string owner) =>
            Web3.Eth.ERC20.GetContractService(Usdc).BalanceOfQueryAsync(owner);

        public Task<BigInteger> PayerAllowanceAsync() =>
            Web3.Eth.ERC20.GetContractService(Usdc).AllowanceQueryAsync(Payer.Address, X402Permit2Addresses.Permit2);

        public async Task<BigInteger> EthBalanceOfAsync(string address) =>
            (await Web3.Eth.GetBalance.SendRequestAsync(address)).Value;

        public async Task<BigInteger> TransactionCountAsync(string address) =>
            (await Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(address, BlockParameter.CreatePending())).Value;

        public async Task<SignedApprovalSpec> ApprovalSpecAsync()
        {
            var baseFee = (await Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest())).BaseFeePerGas.Value;
            return new SignedApprovalSpec(
                ChainId, Usdc, await TransactionCountAsync(Payer.Address), baseFee * 2 + 1_000_000);
        }
    }
}
