using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

public class X402ReferenceReverseInteropTests
{
    private const int ChainId = 84532;
    private const string TokenName = "USD Coin";
    private const string TokenVersion = "2";

    private static string? SignScript => Environment.GetEnvironmentVariable("X402_REF_SIGN_SCRIPT");
    private static string Rpc => Environment.GetEnvironmentVariable("X402_RPC") ?? "http://127.0.0.1:8545";

    [Fact]
    public async Task ReferenceClientPayload_IsVerifiedAndSettledByOurFacilitator()
    {
        var script = SignScript;
        if (string.IsNullOrEmpty(script))
            return;

        var facilitatorKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var facilitator = new Account(facilitatorKey.GetPrivateKey(), ChainId);
        var payerAddress = payerKey.GetPublicAddress();
        var payToAddress = EthECKey.GenerateKey().GetPublicAddress();

        var web3 = new Nethereum.Web3.Web3(facilitator, Rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(facilitator.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(web3, facilitator);
        var tokenAddress = await usdc.DeployAsync(TokenName, "USDC", 6, TokenVersion);
        await usdc.MintAsync(payerAddress, BigInteger.Parse("10000000"));

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = Caip2.FormatEip155(ChainId),
            Amount = "1000000",
            Asset = tokenAddress,
            PayTo = payToAddress,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { AssetTransferMethod = "eip3009", Name = TokenName, Version = TokenVersion }
        };
        var requirementsJson = JsonSerializer.Serialize(requirements);

        var payloadJson = RunReferenceSigner(script, Rpc, payerKey.GetPrivateKey(), requirementsJson);
        var payload = JsonSerializer.Deserialize<PaymentPayload>(payloadJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var service = new X402TransferWithAuthorisation3009Service(
            facilitatorKey.GetPrivateKey(),
            new Dictionary<int, IClient> { [ChainId] = new RpcClient(new Uri(Rpc)) });

        var verify = await service.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, $"our facilitator rejected the reference payload: {verify.InvalidReason}");
        Assert.Equal(payerAddress, verify.Payer);

        var settle = await service.SettlePaymentAsync(payload, requirements);
        Assert.True(settle.Success, $"our facilitator failed to settle the reference payload: {settle.ErrorReason}");
        Assert.StartsWith("0x", settle.Transaction);

        var payToBalance = await usdc.GetBalanceAsync(payToAddress);
        Assert.Equal(BigInteger.Parse("1000000"), payToBalance);
    }

    private static string RunReferenceSigner(string script, string rpc, string payerKey, string requirementsJson)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = System.IO.Path.GetDirectoryName(script)
        };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add(rpc);
        psi.ArgumentList.Add(payerKey);
        psi.ArgumentList.Add(requirementsJson);

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60000);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"reference signer failed (exit {proc.ExitCode}): {stderr}");
        return stdout.Trim();
    }
}
