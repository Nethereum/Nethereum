using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Signer.Bls
{
    /// <summary>
    /// IBls implementation that routes verification requests to a native BLST/MCL backend.
    /// </summary>
    public class NativeBls : IBls
    {
        private readonly INativeBlsBindings _bindings;
        private bool _isInitialized;

        public NativeBls(INativeBlsBindings bindings)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                return;
            }

            await _bindings.EnsureAvailableAsync(cancellationToken).ConfigureAwait(false);
            _isInitialized = true;
        }

        public bool VerifyAggregate(
            byte[] aggregateSignature,
            byte[][] publicKeys,
            byte[][] messages,
            byte[] domain)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NativeBls must be initialized before verifying signatures.");
            }

            if (aggregateSignature == null) throw new ArgumentNullException(nameof(aggregateSignature));
            if (publicKeys == null) throw new ArgumentNullException(nameof(publicKeys));
            if (messages == null) throw new ArgumentNullException(nameof(messages));
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            if (messages.Length != 1 && publicKeys.Length != messages.Length)
            {
                throw new ArgumentException("Public key and message counts must match.", nameof(messages));
            }

            return _bindings.VerifyAggregate(aggregateSignature, publicKeys, messages, domain);
        }
    }
}
