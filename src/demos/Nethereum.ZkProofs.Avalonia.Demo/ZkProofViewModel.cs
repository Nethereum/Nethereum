using System;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.CircomWitnessCalc;
using Nethereum.PrivacyPools.Circuits;
using Nethereum.ZkProofs.RapidSnark;
using Nethereum.ZkProofs.Groth16;
using Nethereum.ZkProofsVerifier.Circom;

namespace Nethereum.ZkProofs.Avalonia.Demo;

public partial class ZkProofViewModel : ObservableObject
{
    private readonly PrivacyPoolCircuitSource _circuitSource = new();

    [ObservableProperty] private string _nullifier = "12345";
    [ObservableProperty] private string _secret = "67890";
    [ObservableProperty] private string _value = "1000000000000000000";
    [ObservableProperty] private string _label = "1";

    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isProving;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasError;

    [ObservableProperty] private long _witnessTimeMs;
    [ObservableProperty] private long _proofTimeMs;
    [ObservableProperty] private long _verifyTimeMs;

    [ObservableProperty] private string? _commitmentHash;
    [ObservableProperty] private string? _nullifierHash;
    [ObservableProperty] private string? _outputValue;
    [ObservableProperty] private string? _outputLabel;
    [ObservableProperty] private string? _proofJson;

    [ObservableProperty] private bool? _verified;
    [ObservableProperty] private string? _verifyError;

    private byte[]? _graphData;
    private byte[]? _zkeyBytes;
    private string? _vkJson;

    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Loading circuit artifacts (graph.bin, zkey, verification key)...";
            HasError = false;

            await Task.Run(async () =>
            {
                _graphData = _circuitSource.GetGraphData("commitment");
                _zkeyBytes = await _circuitSource.GetZkeyAsync("commitment");
                _vkJson = _circuitSource.GetVerificationKeyJson("commitment");
            });

            IsInitialized = true;
            StatusMessage = "Ready. Circuit artifacts loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization failed: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    private async Task GenerateProofAsync()
    {
        if (!IsInitialized || _graphData is null || _zkeyBytes is null || _vkJson is null) return;

        IsProving = true;
        HasError = false;
        CommitmentHash = null;
        NullifierHash = null;
        OutputValue = null;
        OutputLabel = null;
        ProofJson = null;
        Verified = null;
        VerifyError = null;

        try
        {
            var inputJson = JsonSerializer.Serialize(new
            {
                nullifier = Nullifier,
                secret = Secret,
                value = Value,
                label = Label
            });

            var graphData = _graphData;
            var zkeyBytes = _zkeyBytes;
            var vkJson = _vkJson;

            StatusMessage = "Step 1/3: Computing witness (native circom-witnesscalc)...";

            byte[] witnessBytes = null!;
            string proofJsonResult = null!;
            string publicSignalsJson = null!;
            BigInteger[]? publicSignals = null;

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                witnessBytes = WitnessCalculator.CalculateWitness(graphData, inputJson);
                sw.Stop();
                WitnessTimeMs = sw.ElapsedMilliseconds;
            });

            StatusMessage = $"Step 2/3: Generating Groth16 proof (native rapidsnark)...";

            await Task.Run(() =>
            {
                using var prover = new RapidSnarkProver();
                var sw = Stopwatch.StartNew();
                (proofJsonResult, publicSignalsJson) = prover.Prove(zkeyBytes, witnessBytes);
                sw.Stop();
                ProofTimeMs = sw.ElapsedMilliseconds;

                publicSignals = Groth16ProofConverter.ParsePublicSignals(publicSignalsJson);
            });

            ProofJson = proofJsonResult;

            if (publicSignals is { Length: >= 1 })
                CommitmentHash = publicSignals[0].ToString();
            if (publicSignals is { Length: >= 2 })
                NullifierHash = publicSignals[1].ToString();
            if (publicSignals is { Length: >= 3 })
                OutputValue = publicSignals[2].ToString();
            if (publicSignals is { Length: >= 4 })
                OutputLabel = publicSignals[3].ToString();

            StatusMessage = "Step 3/3: Verifying proof (pure C# BN128 pairing)...";

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var verification = CircomGroth16Adapter.Verify(proofJsonResult, vkJson, publicSignalsJson);
                sw.Stop();
                VerifyTimeMs = sw.ElapsedMilliseconds;

                Verified = verification.IsValid;
                if (!verification.IsValid)
                    VerifyError = verification.Error;
            });

            var total = WitnessTimeMs + ProofTimeMs + VerifyTimeMs;
            StatusMessage = $"Complete! Witness: {WitnessTimeMs}ms, Proof: {ProofTimeMs}ms, Verify: {VerifyTimeMs}ms (Total: {total}ms)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsProving = false;
        }
    }
}
