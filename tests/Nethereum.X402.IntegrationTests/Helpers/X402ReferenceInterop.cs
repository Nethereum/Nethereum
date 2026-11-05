using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Testing;
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

namespace Nethereum.X402.IntegrationTests.Helpers;

public enum InteropCase
{
    Eip3009,
    Permit2,
    Permit2Eip2612,
    Permit2Erc20,
    Upfront
}

public sealed record ReferenceClientResult(int Status, string Body, bool SettleSuccess, string Transaction);

public static class X402ReferenceTools
{
    public static string? ForkRpc => TestEnvironment.ServiceOrNull("X402ForkRpc");

    public static string? ScriptDirectory
    {
        get
        {
            var configured = TestEnvironment.ServiceOrNull("X402RefSignScript");
            if (string.IsNullOrEmpty(configured)) return null;
            var script = Path.IsPathRooted(configured) ? configured : Path.Combine(TestEnvironment.RepoRoot, configured);
            return File.Exists(script) ? Path.GetDirectoryName(script) : null;
        }
    }

    public static string? NodeSkipReason(string scriptName)
    {
        var directory = ScriptDirectory;
        if (string.IsNullOrEmpty(ForkRpc))
            return "Services:X402ForkRpc is not set in appsettings.test.local.json; set it to an anvil fork of Base Sepolia to run this test.";
        if (directory == null)
            return "Services:X402RefSignScript is not set or does not exist; set it to the reference signer script to run this test.";
        if (!File.Exists(Path.Combine(directory, scriptName)) || !Directory.Exists(Path.Combine(directory, "node_modules")))
            return $"{scriptName} or its node_modules is missing; run npm ci in {directory}.";
        return null;
    }

    public static string? GoSkipReason()
    {
        var directory = ScriptDirectory;
        if (string.IsNullOrEmpty(ForkRpc) || directory == null) return NodeSkipReason("reference-client.mjs");
        if (!File.Exists(Path.Combine(directory, "go", "go.mod"))) return "ci/externals/x402/go is missing.";
        try
        {
            ReferenceProcess.Run("go", new[] { "version" }, directory);
            return null;
        }
        catch (Exception)
        {
            return "The go toolchain is not installed.";
        }
    }

    public static string? PythonExecutable
    {
        get
        {
            var directory = ScriptDirectory;
            if (directory == null) return null;
            var venv = Path.Combine(directory, "python", ".venv");
            var candidate = OperatingSystem.IsWindows()
                ? Path.Combine(venv, "Scripts", "python.exe")
                : Path.Combine(venv, "bin", "python");
            return File.Exists(candidate) ? candidate : null;
        }
    }

    public static string? PythonSkipReason()
    {
        if (string.IsNullOrEmpty(ForkRpc) || ScriptDirectory == null) return NodeSkipReason("reference-client.mjs");
        return PythonExecutable == null
            ? "The python reference venv is missing; create ci/externals/x402/python/.venv and pip install -r requirements.txt."
            : null;
    }

    public static ReferenceClientResult RunTsClient(string version, string rpc, string payerKey, string url) =>
        Parse(ReferenceProcess.Run("node", new[] { "reference-client.mjs", version, rpc, payerKey, url }, ScriptDirectory!));

    public static ReferenceClientResult RunGoClient(string rpc, string payerKey, string url) =>
        Parse(ReferenceProcess.Run("go", new[] { "run", "./client", rpc, payerKey, url }, Path.Combine(ScriptDirectory!, "go")));

    public static ReferenceClientResult RunPythonClient(string rpc, string payerKey, string url) =>
        Parse(ReferenceProcess.Run(PythonExecutable!, new[] { "reference_client.py", rpc, payerKey, url }, Path.Combine(ScriptDirectory!, "python")));

    public static (ReferenceProcess Process, string Url) StartTsFacilitator(string rpc, string facilitatorKey)
    {
        var port = ReferenceProcess.FreePort();
        return (ReferenceProcess.StartServer("node", new[] { "reference-facilitator.mjs", port.ToString(), rpc, facilitatorKey }, ScriptDirectory!),
            $"http://127.0.0.1:{port}");
    }

    public static (ReferenceProcess Process, string Url) StartGoFacilitator(string rpc, string facilitatorKey)
    {
        var port = ReferenceProcess.FreePort();
        return (ReferenceProcess.StartServer("go", new[] { "run", "./facilitator", port.ToString(), rpc, facilitatorKey }, Path.Combine(ScriptDirectory!, "go")),
            $"http://127.0.0.1:{port}");
    }

    private static ReferenceClientResult Parse(string output)
    {
        using var document = JsonDocument.Parse(output.Trim().Split('\n').Last());
        var root = document.RootElement;
        var success = false;
        var transaction = string.Empty;
        if (root.TryGetProperty("settle", out var settle) && settle.ValueKind == JsonValueKind.Object)
        {
            success = settle.TryGetProperty("success", out var s) && s.GetBoolean();
            transaction = settle.TryGetProperty("transaction", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        }
        return new ReferenceClientResult(root.GetProperty("status").GetInt32(), root.GetProperty("body").GetString() ?? string.Empty, success, transaction);
    }
}

public sealed class ReferenceProcess : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output = new();
    private readonly TaskCompletionSource _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ReferenceProcess(Process process)
    {
        _process = process;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (_output) _output.AppendLine(e.Data);
            if (e.Data.Trim() == "listening") _listening.TrySetResult();
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (_output) _output.AppendLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static string Run(string fileName, IEnumerable<string> arguments, string workingDirectory, int timeoutMs = 300000)
    {
        using var process = Launch(fileName, arguments, workingDirectory);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(true);
            throw new TimeoutException($"{fileName} {string.Join(' ', arguments)} did not finish in {timeoutMs} ms.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} exited {process.ExitCode}: {stderr.Result}{stdout.Result}");
        return stdout.Result;
    }

    public static ReferenceProcess StartServer(string fileName, IEnumerable<string> arguments, string workingDirectory) =>
        new(Launch(fileName, arguments, workingDirectory));

    public async Task WaitUntilListeningAsync(int timeoutMs = 240000)
    {
        var finished = await Task.WhenAny(_listening.Task, Task.Delay(timeoutMs));
        if (finished != _listening.Task)
            throw new TimeoutException($"The reference server did not start listening:{Environment.NewLine}{Output}");
    }

    public string Output
    {
        get
        {
            lock (_output) return _output.ToString();
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(true);
        }
        catch (InvalidOperationException)
        {
        }
        _process.Dispose();
    }

    private static Process Launch(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info)!;
    }
}

public sealed class InteropFork
{
    public const int ChainId = 84532;
    public const string Network = "eip155:84532";
    public const string Usdc = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    public static readonly BigInteger Amount = 1_000_000;
    private const int UsdcBalanceSlot = 9;
    private static readonly BigInteger OneEther = BigInteger.Parse("1000000000000000000");

    private InteropFork(
        string rpc,
        Nethereum.Web3.Web3 web3,
        EthECKey facilitatorKey,
        EthECKey payerKey,
        string recipient,
        Dictionary<int, IClient> clients)
    {
        Rpc = rpc;
        Web3 = web3;
        FacilitatorKey = facilitatorKey;
        PayerKey = payerKey;
        Recipient = recipient;
        Clients = clients;
    }

    public string Rpc { get; }
    public Nethereum.Web3.Web3 Web3 { get; }
    public EthECKey FacilitatorKey { get; }
    public EthECKey PayerKey { get; }
    public string Recipient { get; }
    public Dictionary<int, IClient> Clients { get; }
    public string PayerAddress => PayerKey.GetPublicAddress();

    public static async Task<InteropFork> CreateAsync(string rpc, bool payerApprovesPermit2)
    {
        var facilitatorKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var recipient = EthECKey.GenerateKey().GetPublicAddress();
        var facilitatorAccount = new Account(facilitatorKey.GetPrivateKey(), ChainId);
        var web3 = new Nethereum.Web3.Web3(facilitatorAccount, rpc);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(
            facilitatorAccount.Address, new HexBigInteger(BigInteger.Parse("100000000000000000000")));

        var slot = new HexBigInteger(
            new BigInteger(Sha3Keccack.Current.CalculateHash(
                payerKey.GetPublicAddress().HexToByteArray().PadTo32Bytes()
                    .Concat(new BigInteger(UsdcBalanceSlot).ToByteArray(isUnsigned: true, isBigEndian: true).PadTo32Bytes())
                    .ToArray()),
                isUnsigned: true, isBigEndian: true));
        await web3.Eth.Anvil().SetStorageAt.SendRequestAsync(
            Usdc, slot, (Amount * 10).ToByteArray(isUnsigned: true, isBigEndian: true));

        var fork = new InteropFork(rpc, web3, facilitatorKey, payerKey, recipient, new Dictionary<int, IClient> { [ChainId] = web3.Client });
        if (payerApprovesPermit2)
        {
            var payer = new Account(payerKey.GetPrivateKey(), ChainId);
            await web3.Eth.Anvil().SetBalance.SendRequestAsync(payer.Address, new HexBigInteger(OneEther));
            await new Nethereum.Web3.Web3(payer, rpc).Eth.ERC20.GetContractService(Usdc)
                .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 96));
        }
        return fork;
    }

    public Task<BigInteger> BalanceOfAsync(string owner) =>
        Web3.Eth.ERC20.GetContractService(Usdc).BalanceOfQueryAsync(owner);

    public Task<BigInteger> PayerAllowanceAsync() =>
        Web3.Eth.ERC20.GetContractService(Usdc).AllowanceQueryAsync(PayerAddress, X402Permit2Addresses.Permit2);

    public async Task<BigInteger> EthBalanceOfAsync(string address) =>
        (await Web3.Eth.GetBalance.SendRequestAsync(address)).Value;

    public PaymentRequirements Requirements(InteropCase interopCase) => new()
    {
        Scheme = X402Schemes.Exact,
        Network = Network,
        Amount = Amount.ToString(),
        Asset = Usdc,
        PayTo = Recipient,
        MaxTimeoutSeconds = 3600,
        Extra = interopCase switch
        {
            InteropCase.Eip3009 => new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Eip3009, Name = "USDC", Version = "2" },
            InteropCase.Upfront => new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Eip3009, PaymentFlow = X402PaymentFlows.Upfront, Name = "USDC", Version = "2" },
            _ => new ExactSchemeExtra { AssetTransferMethod = X402AssetTransferMethods.Permit2, Name = "USDC", Version = "2" }
        }
    };

    public IFacilitatorClient InProcessFacilitator()
    {
        var cap = BigInteger.Parse("1000000000000000");
        var processor = new X402ExactSchemeProcessor(
            new X402TransferWithAuthorisation3009Service(FacilitatorKey.GetPrivateKey(), Clients),
            new X402ExactPermit2Service(
                FacilitatorKey.GetPrivateKey(),
                Clients,
                new Erc20ApprovalGasSponsoringOptions { Enabled = true, MaxSponsoredGasCost = cap }));
        return new ProcessorFacilitatorClient(processor);
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
}

public sealed class InteropServer : IDisposable
{
    private readonly IWebHost _host;

    private InteropServer(IWebHost host, string baseUrl)
    {
        _host = host;
        BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    public string Url(InteropCase interopCase) => $"{BaseUrl}/{interopCase}";

    public static InteropServer Start(InteropFork fork, IFacilitatorClient facilitator)
    {
        var baseUrl = $"http://127.0.0.1:{ReferenceProcess.FreePort()}";
        var host = new WebHostBuilder()
            .UseKestrel()
            .UseUrls(baseUrl)
            .ConfigureServices(services => services.AddSingleton(facilitator))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    foreach (var interopCase in Enum.GetValues<InteropCase>())
                        options.Routes.Add(Route(fork, interopCase));
                });
                app.Run(context => context.Response.WriteAsync("premium content"));
            })
            .Build();
        host.Start();
        return new InteropServer(host, baseUrl);
    }

    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }

    private static RoutePaymentConfig Route(InteropFork fork, InteropCase interopCase)
    {
        var route = new RoutePaymentConfig($"/{interopCase}", fork.Requirements(interopCase));
        if (interopCase == InteropCase.Permit2Eip2612)
            route.Extensions = new Dictionary<string, object> { [X402ExtensionKeys.Eip2612GasSponsoring] = X402GasSponsoringExtensions.DeclareEip2612() };
        if (interopCase == InteropCase.Permit2Erc20)
            route.Extensions = new Dictionary<string, object> { [X402ExtensionKeys.Erc20ApprovalGasSponsoring] = X402GasSponsoringExtensions.DeclareErc20Approval() };
        return route;
    }
}

public static class InteropScenario
{
    public static async Task ReferenceClientPaysAsync(
        InteropCase interopCase,
        Func<string, string, string, ReferenceClientResult> referenceClient)
    {
        var fork = await InteropFork.CreateAsync(X402ReferenceTools.ForkRpc!, payerApprovesPermit2: interopCase == InteropCase.Permit2);
        using var server = InteropServer.Start(fork, fork.InProcessFacilitator());

        var result = await Task.Run(() => referenceClient(fork.Rpc, fork.PayerKey.GetPrivateKey(), server.Url(interopCase)));

        Assert.True(result.Status == 200, $"HTTP {result.Status}: {result.Body}");
        Assert.True(result.SettleSuccess, $"no successful PAYMENT-RESPONSE: {result.Body}");
        Assert.Equal(InteropFork.Amount, await fork.BalanceOfAsync(fork.Recipient));
        await AssertSponsoringPathAsync(fork, interopCase);
    }

    public static async Task NethereumClientPaysAsync(
        InteropCase interopCase,
        Func<string, string, (ReferenceProcess Process, string Url)> startReferenceFacilitator)
    {
        var fork = await InteropFork.CreateAsync(X402ReferenceTools.ForkRpc!, payerApprovesPermit2: interopCase == InteropCase.Permit2);
        var (process, facilitatorUrl) = startReferenceFacilitator(fork.Rpc, fork.FacilitatorKey.GetPrivateKey());
        using var reference = process;
        await reference.WaitUntilListeningAsync();
        using var facilitatorHttp = new HttpClient();
        using var server = InteropServer.Start(fork, new HttpFacilitatorClient(facilitatorHttp, facilitatorUrl));
        using var http = new HttpClient();
        var client = new X402HttpClient(http, fork.PayerKey.GetPrivateKey(), new X402HttpClientOptions
        {
            PreferredNetwork = InteropFork.Network,
            PreferredScheme = X402Schemes.Exact,
            MaxAmount = "10000000",
            RpcClientsByChainId = new Dictionary<int, IClient> { [InteropFork.ChainId] = new RpcClient(new Uri(fork.Rpc)) }
        });

        var response = await client.GetAsync(server.Url(interopCase));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"HTTP {(int)response.StatusCode}: {body}{Environment.NewLine}{reference.Output}");
        Assert.Equal(InteropFork.Amount, await fork.BalanceOfAsync(fork.Recipient));
        await AssertSponsoringPathAsync(fork, interopCase);
    }

    private static async Task AssertSponsoringPathAsync(InteropFork fork, InteropCase interopCase)
    {
        var allowance = await fork.PayerAllowanceAsync();
        if (interopCase == InteropCase.Permit2Eip2612)
            Assert.True(allowance == BigInteger.Zero, $"the permit should be consumed by the transfer, allowance is {allowance}");
        if (interopCase == InteropCase.Permit2Erc20)
            Assert.True(allowance > BigInteger.Zero, "the relayed approval should leave a Permit2 allowance");
    }
}
