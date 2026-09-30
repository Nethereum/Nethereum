using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Compliance
{
    [Trait("Category", "Compliance")]
    public class BundlerSpecComplianceGateTests
    {
        private static readonly Regex SummaryLineRegex = new Regex(
            @"(?:\d+\s+(?:failed|passed|skipped|deselected|xfailed|xpassed|warnings?|errors?)(?:,\s*)?)+\s*in\s+[\d.]+s",
            RegexOptions.Compiled);

        private static readonly Regex PassedTokenRegex = new Regex(
            @"(\d+)\s+passed", RegexOptions.Compiled);

        private static readonly Regex FailedTokenRegex = new Regex(
            @"(\d+)\s+failed", RegexOptions.Compiled);

        private static readonly Regex ErrorsTokenRegex = new Regex(
            @"(\d+)\s+errors?\b", RegexOptions.Compiled);

        private static readonly Regex AnsiRegex = new Regex(
            @"\x1b\[[0-9;]*m", RegexOptions.Compiled);

        [SkippableFact]
        [Trait("Category", "Compliance")]
        public void EthInfinitismComplianceSuite_RunsWithZeroFailures()
        {
            var specTests = Environment.GetEnvironmentVariable("BUNDLER_SPEC_TESTS");
            Skip.If(string.IsNullOrEmpty(specTests) || !Directory.Exists(specTests),
                "Compliance gate skipped: set BUNDLER_SPEC_TESTS to a bundler-spec-tests clone " +
                "(with its .venv and spec/openrpc.json built — see " +
                "tests/Nethereum.AccountAbstraction.ComplianceHarness/README.md), then run " +
                "`dotnet test --filter Category=Compliance`. This gate MUST pass before any " +
                "bundler change is called done.");

            Skip.If(!IsOnPath("anvil", "--version"),
                "Compliance gate skipped: `anvil` is not on PATH. Install Foundry " +
                "(https://getfoundry.sh — `curl -L https://foundry.paradigm.xyz | bash && foundryup`) " +
                "then run `dotnet test --filter Category=Compliance`.");

            Skip.If(!IsOnPath("pwsh", "-Version"),
                "Compliance gate skipped: `pwsh` (PowerShell 7) is not on PATH. The compliance " +
                "harness is a .ps1 script and requires pwsh. Install it " +
                "(https://aka.ms/powershell) then run `dotnet test --filter Category=Compliance`.");

            var venvPath = Path.Combine(specTests, ".venv");
            Skip.If(!Directory.Exists(venvPath),
                $"Compliance gate skipped: no Python venv found at {venvPath}. Build it per " +
                "tests/Nethereum.AccountAbstraction.ComplianceHarness/README.md " +
                "(`python -m venv .venv` + `pip install -r requirements.txt` inside the " +
                "bundler-spec-tests clone) then run `dotnet test --filter Category=Compliance`.");

            var repoRoot = FindRepoRoot();
            Skip.If(repoRoot == null,
                "Compliance gate skipped: could not locate " +
                "tests/Nethereum.AccountAbstraction.ComplianceHarness/run-spec-tests.ps1 by " +
                "walking up from AppContext.BaseDirectory — repo layout may have changed.");

            var script = Path.Combine(repoRoot!,
                "tests", "Nethereum.AccountAbstraction.ComplianceHarness", "run-spec-tests.ps1");

            var (output, timedOut) = RunComplianceHarness(script, specTests!, repoRoot!);

            if (timedOut)
            {
                Assert.Fail("eth-infinitism compliance suite timed out after 35 minutes. Tail:\n" +
                    TailLines(output, 50));
            }

            var cleaned = AnsiRegex.Replace(output, string.Empty);
            var match = SummaryLineRegex.Match(cleaned);
            if (!match.Success)
            {
                Assert.Fail("Could not parse pytest summary from compliance output. Tail:\n" +
                    TailLines(cleaned, 50));
            }

            var summaryLine = match.Value;
            var passedMatch = PassedTokenRegex.Match(summaryLine);
            var failedMatch = FailedTokenRegex.Match(summaryLine);
            var errorsMatch = ErrorsTokenRegex.Match(summaryLine);
            var passed = passedMatch.Success ? int.Parse(passedMatch.Groups[1].Value) : 0;
            var failed = failedMatch.Success ? int.Parse(failedMatch.Groups[1].Value) : 0;
            var errors = errorsMatch.Success ? int.Parse(errorsMatch.Groups[1].Value) : 0;
            var failedLines = ExtractFailedLines(cleaned);

            Assert.True(failed == 0 && errors == 0,
                $"eth-infinitism compliance suite reported {failed} failure(s) and {errors} error(s):\n{failedLines}\n" +
                $"Summary: {summaryLine}");

            Assert.True(passed >= 400,
                $"Only {passed} passed — the suite likely did not run to completion " +
                $"(harness/anvil crash?). Tail:\n" + TailLines(cleaned, 50));
        }

        private static (string Output, bool TimedOut) RunComplianceHarness(
            string script, string specTests, string repoRoot)
        {
            var psi = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add("-SpecTests");
            psi.ArgumentList.Add(specTests);
            psi.ArgumentList.Add("-TestPath");
            psi.ArgumentList.Add("tests/single");

            var output = new StringBuilder();
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited = process.WaitForExit((int)TimeSpan.FromMinutes(35).TotalMilliseconds);
            if (!exited)
            {
                try { process.Kill(true); } catch { }
                return (output.ToString(), true);
            }
            return (output.ToString(), false);
        }

        private static string ExtractFailedLines(string output)
        {
            var lines = output.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.TrimStart().StartsWith("FAILED", StringComparison.Ordinal) ||
                    trimmed.TrimStart().StartsWith("ERROR", StringComparison.Ordinal))
                {
                    sb.AppendLine(trimmed);
                }
            }
            return sb.Length == 0 ? "(no FAILED/ERROR lines found in short test summary)" : sb.ToString();
        }

        private static string TailLines(string text, int count)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var start = Math.Max(0, lines.Length - count);
            return string.Join("\n", lines, start, lines.Length - start);
        }

        private static bool IsOnPath(string fileName, string versionArg)
        {
            try
            {
                var psi = new ProcessStartInfo(fileName, versionArg)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null) return false;
                process.WaitForExit(10_000);
                return process.HasExited && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string? FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName,
                    "tests", "Nethereum.AccountAbstraction.ComplianceHarness", "run-spec-tests.ps1");
                if (File.Exists(candidate))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
