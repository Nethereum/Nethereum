using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Signer.Bls;
using mcl;

namespace Nethereum.Signer.Bls.Herumi
{
    /// <summary>
    /// Adapter that wires the Herumi/MCL ETH-mode bindings into <see cref="INativeBlsBindings"/>.
    /// </summary>
    public class HerumiNativeBindings : INativeBlsBindings
    {
        private static readonly object InitLock = new object();
        private static bool _initialised;

        private static void EnsureInitialized()
        {
            if (_initialised)
            {
                return;
            }

            lock (InitLock)
            {
                if (_initialised)
                {
                    return;
                }

                // Herumi's ETH-flavoured build maps to BLS12-381.
                BLS.Init(BLS.BLS12_381);
                _initialised = true;
            }
        }

        public Task EnsureAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            return Task.CompletedTask;
        }

        public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain)
        {
            if (aggregateSignature == null || aggregateSignature.Length == 0)
            {
                throw new ArgumentException("Aggregate signature payload is required.", nameof(aggregateSignature));
            }

            if (publicKeys == null || publicKeys.Length == 0)
            {
                throw new ArgumentException("At least one public key is required.", nameof(publicKeys));
            }

            if (messages == null || messages.Length == 0)
            {
                throw new ArgumentException("At least one message is required.", nameof(messages));
            }

            ValidateDomain(domain);

            var signature = DeserializeSignature(aggregateSignature);
            var herumiPublicKeys = DeserializePublicKeys(publicKeys);

            if (messages.Length == 1)
            {
                var message = PrepareSingleMessage(messages[0]);
                return BLS.FastAggregateVerify(in signature, in herumiPublicKeys, message);
            }

            if (messages.Length != herumiPublicKeys.Length)
            {
                throw new ArgumentException("Multi-message aggregates require a 1:1 mapping between messages and public keys.", nameof(messages));
            }

            var msgVector = PrepareMessageVector(messages);
            return BLS.AggregateVerify(in signature, in herumiPublicKeys, in msgVector);
        }

        private static BLS.Signature DeserializeSignature(byte[] payload)
        {
            var signature = new BLS.Signature();
            signature.Deserialize(payload);
            return signature;
        }

        private static BLS.PublicKey[] DeserializePublicKeys(byte[][] publicKeys)
        {
            var result = new BLS.PublicKey[publicKeys.Length];
            for (var i = 0; i < publicKeys.Length; i++)
            {
                var pk = new BLS.PublicKey();
                pk.Deserialize(publicKeys[i]);
                result[i] = pk;
            }

            return result;
        }

        private static void ValidateDomain(byte[] domain)
        {
            if (domain == null || domain.Length == 0)
            {
                return;
            }

            if (domain.Length != 32)
            {
                throw new ArgumentException("Domains must be 32 bytes when provided.", nameof(domain));
            }
        }

        private static byte[] PrepareSingleMessage(byte[] message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Length != BLS.MSG_SIZE)
            {
                throw new ArgumentException($"Messages must be {BLS.MSG_SIZE} bytes for ETH mode.", nameof(message));
            }

            return message;
        }

        private static BLS.Msg[] PrepareMessageVector(byte[][] messages)
        {
            var msgVector = new BLS.Msg[messages.Length];

            for (var i = 0; i < messages.Length; i++)
            {
                var msg = new BLS.Msg();
                msg.Set(PrepareSingleMessage(messages[i]));
                msgVector[i] = msg;
            }

            return msgVector;
        }
    }
}
