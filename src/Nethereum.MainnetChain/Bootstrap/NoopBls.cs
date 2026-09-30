using System;
using Nethereum.Signer.Bls;

namespace Nethereum.MainnetChain.Bootstrap
{
    internal sealed class NoopBls : IBls
    {
        public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;
        public byte[] AggregateSignatures(byte[][] signatures) => Array.Empty<byte>();
        public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => true;
        public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey)
            => (Array.Empty<byte>(), Array.Empty<byte>());
    }
}
