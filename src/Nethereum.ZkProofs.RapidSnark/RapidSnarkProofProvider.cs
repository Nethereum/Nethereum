using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ZkProofs;

namespace Nethereum.ZkProofs.RapidSnark
{
    public class RapidSnarkProofProvider : IZkProofProvider
    {
        public ZkProofScheme Scheme => ZkProofScheme.Groth16;

        public Task<ZkProofResult> FullProveAsync(ZkProofRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (request.WitnessBytes == null || request.WitnessBytes.Length == 0)
                throw new ArgumentException(
                    "RapidSnark requires pre-computed witness bytes. Set ZkProofRequest.WitnessBytes with the .wtns binary.",
                    nameof(request));

            if (request.CircuitZkey == null || request.CircuitZkey.Length == 0)
                throw new ArgumentException(
                    "Circuit zkey must be provided.",
                    nameof(request));

            cancellationToken.ThrowIfCancellationRequested();

            using var prover = new RapidSnarkProver();
            var (proofJson, publicJson) = prover.Prove(request.CircuitZkey, request.WitnessBytes);

            return Task.FromResult(ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, proofJson, publicJson));
        }
    }
}
