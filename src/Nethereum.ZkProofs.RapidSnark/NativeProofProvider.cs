using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CircomWitnessCalc;
using Nethereum.ZkProofs;

namespace Nethereum.ZkProofs.RapidSnark
{
    public class NativeProofProvider : IZkProofProvider
    {
        private readonly ICircuitGraphSource? _graphSource;

        public ZkProofScheme Scheme => ZkProofScheme.Groth16;

        public NativeProofProvider()
        {
        }

        public NativeProofProvider(ICircuitGraphSource graphSource)
        {
            _graphSource = graphSource ?? throw new ArgumentNullException(nameof(graphSource));
        }

        public Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.CircuitZkey == null || request.CircuitZkey.Length == 0)
                throw new ArgumentException("Circuit zkey must be provided.", nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            byte[] witnessBytes;

            if (request.WitnessBytes != null && request.WitnessBytes.Length > 0)
            {
                witnessBytes = request.WitnessBytes;
            }
            else if (request.CircuitGraph != null && request.CircuitGraph.Length > 0
                     && !string.IsNullOrEmpty(request.InputJson))
            {
                witnessBytes = WitnessCalculator.CalculateWitness(request.CircuitGraph, request.InputJson);
            }
            else if (_graphSource != null
                     && !string.IsNullOrEmpty(request.InputJson)
                     && !string.IsNullOrEmpty(request.CircuitName))
            {
                var graphData = _graphSource.GetGraphData(request.CircuitName);
                witnessBytes = WitnessCalculator.CalculateWitness(graphData, request.InputJson);
            }
            else
            {
                throw new ArgumentException(
                    "NativeProofProvider requires either: " +
                    "(1) pre-computed WitnessBytes, " +
                    "(2) CircuitGraph + InputJson, or " +
                    "(3) an ICircuitGraphSource + CircuitName + InputJson.",
                    nameof(request));
            }

            using var prover = new RapidSnarkProver();
            var (proofJson, publicJson) = prover.Prove(request.CircuitZkey, witnessBytes);

            return Task.FromResult(ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, proofJson, publicJson));
        }
    }
}
