using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Signer.Bls
{
    public interface INativeBlsBindings
    {
        Task EnsureAvailableAsync(CancellationToken cancellationToken);

        bool VerifyAggregate(
            byte[] aggregateSignature,
            byte[][] publicKeys,
            byte[][] messages,
            byte[] domain);

        byte[] AggregateSignatures(byte[][] signatures);

        bool Verify(byte[] signature, byte[] publicKey, byte[] message);
    }
}
