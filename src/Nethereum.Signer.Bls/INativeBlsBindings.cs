using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Signer.Bls
{
    /// <summary>
    /// Represents the low-level BLST/MCL bindings used by <see cref="NativeBls"/>.
    /// </summary>
    public interface INativeBlsBindings
    {
        Task EnsureAvailableAsync(CancellationToken cancellationToken);

        bool VerifyAggregate(
            byte[] aggregateSignature,
            byte[][] publicKeys,
            byte[][] messages,
            byte[] domain);
    }
}
