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

        public byte[] AggregateSignatures(byte[][] signatures)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NativeBls must be initialized before aggregating signatures.");
            }

            if (signatures == null) throw new ArgumentNullException(nameof(signatures));
            if (signatures.Length == 0) throw new ArgumentException("At least one signature is required.", nameof(signatures));

            return _bindings.AggregateSignatures(signatures);
        }

        public bool Verify(byte[] signature, byte[] publicKey, byte[] message)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("NativeBls must be initialized before verifying signatures.");
            }

            if (signature == null) throw new ArgumentNullException(nameof(signature));
            if (publicKey == null) throw new ArgumentNullException(nameof(publicKey));
            if (message == null) throw new ArgumentNullException(nameof(message));

            return _bindings.Verify(signature, publicKey, message);
        }

        public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey)
        {
            if (signatureWithPubKey == null) throw new ArgumentNullException(nameof(signatureWithPubKey));

            const int SignatureSize = 96;
            const int PublicKeySize = 48;

            if (signatureWithPubKey.Length < SignatureSize + PublicKeySize)
            {
                throw new ArgumentException(
                    $"Combined signature must be at least {SignatureSize + PublicKeySize} bytes (96 for sig + 48 for pubkey).",
                    nameof(signatureWithPubKey));
            }

            var signature = new byte[SignatureSize];
            var publicKey = new byte[PublicKeySize];

            Array.Copy(signatureWithPubKey, 0, signature, 0, SignatureSize);
            Array.Copy(signatureWithPubKey, SignatureSize, publicKey, 0, PublicKeySize);

            return (signature, publicKey);
        }
    }
}
