using System;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ZkProofs;

namespace Nethereum.PrivacyPools
{
    public class PrivacyPoolProofProvider : IPrivacyPoolProofProvider
    {
        private readonly IZkProofProvider _provider;
        private readonly ICircuitArtifactSource _artifactSource;
        private readonly ICircuitGraphSource? _graphSource;

        private const string COMMITMENT_CIRCUIT = "commitment";
        private const string WITHDRAWAL_CIRCUIT = "withdrawal";

        public PrivacyPoolProofProvider(IZkProofProvider provider, ICircuitArtifactSource artifactSource)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _artifactSource = artifactSource ?? throw new ArgumentNullException(nameof(artifactSource));
            _graphSource = artifactSource as ICircuitGraphSource;
        }

        public async Task<RagequitProofResult> GenerateRagequitProofAsync(RagequitWitnessInput input, CancellationToken cancellationToken = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var inputJson = SerializeRagequitInput(input);
            var wasm = await _artifactSource.GetWasmAsync(COMMITMENT_CIRCUIT, cancellationToken);
            var zkey = await _artifactSource.GetZkeyAsync(COMMITMENT_CIRCUIT, cancellationToken);

            var request = new ZkProofRequest
            {
                CircuitWasm = wasm,
                CircuitZkey = zkey,
                InputJson = inputJson,
                CircuitName = COMMITMENT_CIRCUIT,
                Scheme = _provider.Scheme
            };

            if (_graphSource != null && _graphSource.HasGraph(COMMITMENT_CIRCUIT))
                request.CircuitGraph = _graphSource.GetGraphData(COMMITMENT_CIRCUIT);

            var result = await _provider.FullProveAsync(request, cancellationToken);

            var signals = RagequitProofSignals.FromArray(result.PublicSignals);

            return new RagequitProofResult
            {
                ProofJson = result.ProofJson,
                PublicJson = result.PublicSignalsJson,
                Signals = signals
            };
        }

        public async Task<WithdrawalProofResult> GenerateWithdrawalProofAsync(WithdrawalWitnessInput input, CancellationToken cancellationToken = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var inputJson = SerializeWithdrawalInput(input);
            var wasm = await _artifactSource.GetWasmAsync(WITHDRAWAL_CIRCUIT, cancellationToken);
            var zkey = await _artifactSource.GetZkeyAsync(WITHDRAWAL_CIRCUIT, cancellationToken);

            var request = new ZkProofRequest
            {
                CircuitWasm = wasm,
                CircuitZkey = zkey,
                InputJson = inputJson,
                CircuitName = WITHDRAWAL_CIRCUIT,
                Scheme = _provider.Scheme
            };

            if (_graphSource != null && _graphSource.HasGraph(WITHDRAWAL_CIRCUIT))
                request.CircuitGraph = _graphSource.GetGraphData(WITHDRAWAL_CIRCUIT);

            var result = await _provider.FullProveAsync(request, cancellationToken);

            var signals = WithdrawProofSignals.FromArray(result.PublicSignals);

            return new WithdrawalProofResult
            {
                ProofJson = result.ProofJson,
                PublicJson = result.PublicSignalsJson,
                Signals = signals
            };
        }

        private static string SerializeRagequitInput(RagequitWitnessInput input)
        {
            var obj = new
            {
                nullifier = input.Nullifier.ToString(),
                secret = input.Secret.ToString(),
                value = input.Value.ToString(),
                label = input.Label.ToString()
            };
            return JsonSerializer.Serialize(obj);
        }

        private static string SerializeWithdrawalInput(WithdrawalWitnessInput input)
        {
            var obj = new
            {
                existingValue = input.ExistingValue.ToString(),
                existingNullifier = input.ExistingNullifier.ToString(),
                existingSecret = input.ExistingSecret.ToString(),
                label = input.Label.ToString(),
                newNullifier = input.NewNullifier.ToString(),
                newSecret = input.NewSecret.ToString(),
                withdrawnValue = input.WithdrawnValue.ToString(),
                stateRoot = input.StateRoot.ToString(),
                stateTreeDepth = input.StateTreeDepth.ToString(),
                stateSiblings = Array.ConvertAll(input.StateSiblings, s => s.ToString()),
                stateIndex = input.StateIndex.ToString(),
                ASPRoot = input.ASPRoot.ToString(),
                ASPTreeDepth = input.ASPTreeDepth.ToString(),
                ASPSiblings = Array.ConvertAll(input.ASPSiblings, s => s.ToString()),
                ASPIndex = input.ASPIndex.ToString(),
                context = input.Context.ToString()
            };
            return JsonSerializer.Serialize(obj);
        }
    }
}
