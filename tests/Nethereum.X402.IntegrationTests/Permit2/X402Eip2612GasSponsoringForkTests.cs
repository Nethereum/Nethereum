using System.Numerics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI.EIP712.EIP2612;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Client;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Nethereum.X402.Processors;
using Nethereum.X402.Server;
using Xunit;
using Nethereum.Testing;

namespace Nethereum.X402.IntegrationTests.Permit2;

[Trait("Category", "LiveNetwork")]
public class X402Eip2612GasSponsoringForkTests
{
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private const string Usdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const int UsdcBalanceSlot = 9;
    private const string Key = X402ExtensionKeys.Eip2612GasSponsoring;
    private static readonly BigInteger Amount = 1_000_000;

    private static string? ForkRpc => TestEnvironment.ServiceOrNull("X402ForkRpc");

    [SkippableFact]
    public async Task Given_NoPermit2Allowance_And_Eip2612Declared_When_ClientPays_Then_SettleWithPermitMovesTheAmount()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc);
        Assert.Equal(BigInteger.Zero, await setup.PayerAllowanceAsync());
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
        Assert.Equal(BigInteger.Zero, await setup.PayerAllowanceAsync());
        Assert.Equal(BigInteger.Zero, await setup.EthBalanceOfAsync(setup.Payer.Address));
    }

    [SkippableFact]
    public async Task Given_PermitValueDiffersFromThePermittedAmount_When_Verifying_Then_Permit2612AmountMismatch()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc);
        var payload = await SignedPayloadAsync(setup, permitValue: Amount + 1);

        var verify = await setup.Facilitator.VerifyPaymentAsync(payload, setup.Requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Permit2612AmountMismatch, verify.InvalidReason);
    }

    [SkippableFact]
    public async Task Given_PermitSignedByAnotherKeyAndNoAllowance_When_Verifying_Then_Permit2AllowanceRequired()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc);
        var payload = await SignedPayloadAsync(setup, permitValue: Amount, permitSigner: EthECKey.GenerateKey());

        var verify = await setup.Facilitator.VerifyPaymentAsync(payload, setup.Requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.Permit2AllowanceRequired, verify.InvalidReason);
    }

    [SkippableFact]
    public async Task Given_ValidSignedPermit_When_VerifyingAndSettlingTheWirePayload_Then_TheAmountMoves()
    {
        var rpc = ForkRpc;
        Skip.If(string.IsNullOrEmpty(rpc),
            "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.");

        var setup = await SetUpAsync(rpc);
        var payload = await SignedPayloadAsync(setup, permitValue: Amount);
        var wire = JsonSerializer.Deserialize<PaymentPayload>(JsonSerializer.Serialize(payload))!;

        var verify = await setup.Facilitator.VerifyPaymentAsync(wire, setup.Requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);
        var settle = await setup.Facilitator.SettlePaymentAsync(wire, setup.Requirements);

        Assert.True(settle.Success, settle.ErrorReason);
        Assert.Equal(Amount, await setup.BalanceOfAsync(setup.Recipient));
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
                        Extensions = new Dictionary<string, object> { [Key] = X402GasSponsoringExtensions.DeclareEip2612() }
                    });
                });
                app.Run(context => context.Response.WriteAsync("premium content"));
            });
        return new TestServer(builder);
    }

    private static async Task<PaymentPayload> SignedPayloadAsync(Setup setup, BigInteger permitValue, EthECKey? permitSigner = null)
    {
        var nonceBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonceBytes);
        var permit2Nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = new BigInteger(now + 3600);
        var validAfter = new BigInteger(now - 600);

        var authorization = new Permit2Authorization
        {
            From = setup.Payer.Address,
            Permitted = new Permit2TokenPermissions { Token = Usdc, Amount = Amount.ToString() },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = permit2Nonce.ToString(),
            Deadline = deadline.ToString(),
            Witness = new Permit2WitnessData { To = setup.Recipient, ValidAfter = validAfter.ToString() }
        };
        var signature = new Permit2WitnessSigner().Sign(
            authorization.ToWitnessMessage(), ChainId, X402Permit2Addresses.Permit2, setup.PayerKey);

        var tokenNonce = await setup.TokenNonceAsync();
        var permit = new Permit
        {
            Owner = setup.Payer.Address,
            Spender = X402Permit2Addresses.Permit2,
            Value = permitValue,
            Nonce = tokenNonce,
            Deadline = deadline
        };
        var permitSignature = new Eip2612PermitSigner().Sign(
            permit, "USDC", "2", ChainId, Usdc, permitSigner ?? setup.PayerKey);

        var info = new Eip2612GasSponsoringInfo
        {
            From = setup.Payer.Address,
            Asset = Usdc,
            Spender = X402Permit2Addresses.Permit2,
            Amount = permitValue.ToString(),
            Nonce = tokenNonce.ToString(),
            Deadline = deadline.ToString(),
            Signature = permitSignature,
            Version = "1"
        };

        return new PaymentPayload
        {
            X402Version = X402Protocol.Version,
            Accepted = setup.Requirements,
            Payload = new Permit2SchemePayload { Signature = signature, Permit2Authorization = authorization },
            Extensions = new Dictionary<string, object> { [Key] = new Dictionary<string, object> { ["info"] = info } }
        };
    }

    private static async Task<Setup> SetUpAsync(string rpc)
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

        var setup = new Setup(web3, facilitatorKey, payerKey, payer, recipient, clients, requirements,
            new X402ExactPermit2Service(facilitatorKey.GetPrivateKey(), clients));
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

        public Task<BigInteger> TokenNonceAsync() =>
            Web3.Eth.GetContractHandler(Usdc)
                .QueryAsync<Eip2612NoncesFunction, BigInteger>(new Eip2612NoncesFunction { Owner = Payer.Address });

        public async Task<BigInteger> EthBalanceOfAsync(string address) =>
            (await Web3.Eth.GetBalance.SendRequestAsync(address)).Value;
    }
}
