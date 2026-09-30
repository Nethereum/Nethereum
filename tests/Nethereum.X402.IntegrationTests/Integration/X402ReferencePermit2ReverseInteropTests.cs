using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

public class X402ReferencePermit2ReverseInteropTests
{
    private const int ChainId = 84532;

    private static string? SignScript => Environment.GetEnvironmentVariable("X402_REF_SIGN_SCRIPT");
    private static string Rpc => Environment.GetEnvironmentVariable("X402_RPC") ?? "http://127.0.0.1:8545";

    [Fact]
    public async Task ReferenceClientPermit2Payload_IsVerifiedAndSettledByOurFacilitator()
    {
        var script = SignScript;
        if (string.IsNullOrEmpty(script))
            return;

        var facilitatorKey = EthECKey.GenerateKey();
        var deployerKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var facilitator = new Account(facilitatorKey.GetPrivateKey(), ChainId);
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);
        var payer = new Account(payerKey.GetPrivateKey(), ChainId);
        var payTo = EthECKey.GenerateKey().GetPublicAddress();

        var web3 = new Nethereum.Web3.Web3(deployer, Rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(facilitator.Address, oneHundredEth);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(payer.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(web3, deployer);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");
        await usdc.MintAsync(payer.Address, BigInteger.Parse("10000000"));

        var payerWeb3 = new Nethereum.Web3.Web3(payer, Rpc);
        await payerWeb3.Eth.ERC20.GetContractService(token)
            .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 96));

        var amount = BigInteger.Parse("1000000");
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "eip155:84532",
            Amount = amount.ToString(),
            Asset = token,
            PayTo = payTo,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2" }
        };
        var requirementsJson = JsonSerializer.Serialize(requirements);

        var payloadJson = RunReferenceSigner(script, Rpc, payerKey.GetPrivateKey(), requirementsJson);
        var payload = JsonSerializer.Deserialize<PaymentPayload>(payloadJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var processor = new X402ExactPermit2Service(
            facilitatorKey.GetPrivateKey(),
            new Dictionary<int, IClient> { [ChainId] = new RpcClient(new Uri(Rpc)) });

        var verify = await processor.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, $"our facilitator rejected the reference permit2 payload: {verify.InvalidReason}");
        Assert.Equal(payer.Address, verify.Payer);

        var settle = await processor.SettlePaymentAsync(payload, requirements);
        Assert.True(settle.Success, $"our facilitator failed to settle the reference permit2 payload: {settle.ErrorReason}");
        Assert.StartsWith("0x", settle.Transaction);

        Assert.Equal(amount, await usdc.GetBalanceAsync(payTo));
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
