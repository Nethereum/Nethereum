using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.MainnetChain.Configuration;
using Nethereum.Model;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

[Collection("Sequential")]
public class MainnetServerMaintenanceExitTests : IDisposable
{
    private const string Address = "0x7777777777777777777777777777777777777777";
    private static readonly TimeSpan ExitDeadline = TimeSpan.FromSeconds(90);

    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"mainnet_server_maint_{Guid.NewGuid():N}");

    private string DataDir => Path.Combine(_workDir, "data");

    public MainnetServerMaintenanceExitTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Given_APopulatedStore_When_TheServerRunsWithWipeState_Then_ItLogsWipeProgressExitsZeroAndStartsNoSync()
    {
        var config = new MainnetChainServerConfig { DataDir = DataDir };
        await using (var storage = ChainNodeStorage.Open(config.ToChainNodeConfig()))
        {
            await storage.Bundle.State.SaveAccountAsync(Address, new Account { Balance = 5, Nonce = 1 });
            storage.Bundle.Metadata.SetLastFetchedBody(42);
        }

        var run = await RunServerAsync("--wipe-state", "--allow-unverified-consensus", "--port", FreePort().ToString());

        Assert.True(run.Exited, "the server kept running after --wipe-state instead of exiting:\n" + run.Output);
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("state.wipe", run.Output);
        Assert.Contains("state_trie_account", run.Output);
        Assert.Contains("wipe done", run.Output);
        Assert.DoesNotContain("PeerPool started", run.Output);
        Assert.DoesNotContain("MainnetChain follower starting", run.Output);
        await using var reopened = ChainNodeStorage.Open(config.ToChainNodeConfig());
        Assert.Null(await reopened.Bundle.State.GetAccountAsync(Address));
        Assert.Equal(42UL, reopened.Bundle.Metadata.GetLastFetchedBody());
    }

    [Theory]
    [InlineData("--compact-all")]
    [InlineData("--rebuild-state-from-flat")]
    [InlineData("--verify-flat")]
    public async Task Given_AnotherMaintenanceOp_When_TheServerRunsIt_Then_ItReportsCompletionExitsZeroAndStartsNoSync(string flag)
    {
        var run = await RunServerAsync(flag, "--allow-unverified-consensus", "--port", FreePort().ToString());

        Assert.True(run.Exited, $"the server kept running after {flag} instead of exiting:\n" + run.Output);
        Assert.Equal(0, run.ExitCode);
        Assert.Contains("requested operation(s) completed", run.Output);
        Assert.DoesNotContain("PeerPool started", run.Output);
    }

    [Fact]
    public async Task Given_NoMaintenanceFlag_When_TheServerStarts_Then_ItStartsTheNodeAndKeepsRunning()
    {
        var run = await RunServerAsync(
            new[]
            {
                "--allow-unverified-consensus", "--port", FreePort().ToString(),
                "--MainnetChain:DisableDiscv4", "true", "--MainnetChain:DisableDiscv5", "true",
            },
            stopWhen: output => output.Contains("PeerPool started"));

        Assert.False(run.Exited, "the server exited without a maintenance flag:\n" + run.Output);
        Assert.Contains("PeerPool started", run.Output);
        Assert.DoesNotContain("state.wipe", run.Output);
    }

    private Task<ServerRun> RunServerAsync(params string[] args) => RunServerAsync(args, stopWhen: null);

    private async Task<ServerRun> RunServerAsync(string[] args, Func<string, bool>? stopWhen)
    {
        var serverDll = Path.Combine(AppContext.BaseDirectory, "Nethereum.MainnetChain.Server.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(serverDll);
        startInfo.ArgumentList.Add("--data-dir");
        startInfo.ArgumentList.Add(DataDir);
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        var lines = new ConcurrentQueue<string>();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lines.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lines.Enqueue(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var deadline = Stopwatch.StartNew();
        while (!process.HasExited && deadline.Elapsed < ExitDeadline)
        {
            if (stopWhen != null && stopWhen(string.Join("\n", lines))) break;
            await Task.Delay(250);
        }

        var exited = process.HasExited;
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        else
        {
            process.WaitForExit();
        }

        return new ServerRun(exited, exited ? process.ExitCode : (int?)null, string.Join("\n", lines));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record ServerRun(bool Exited, int? ExitCode, string Output);
}
