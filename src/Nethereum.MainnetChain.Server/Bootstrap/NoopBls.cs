using System;
using Nethereum.Signer.Bls;

namespace Nethereum.MainnetChain.Server.Bootstrap
{
    /// <summary>
    /// Placeholder <see cref="IBls"/> used to register <see cref="Nethereum.Consensus.LightClient.LightClientService"/>
    /// in the DI graph when the operator has configured a beacon endpoint but no
    /// native BLS bindings are available. Accepts all signatures — the light
    /// client's BLS-quorum checks degrade to "all-trusted" in this mode, which
    /// is appropriate when the beacon endpoint itself is trusted (operator's
    /// own Lighthouse, chainsafe public endpoint, etc.).
    ///
    /// <para>Real production deployments should swap in
    /// <see cref="NativeBls"/> with the appropriate
    /// <c>INativeBlsBindings</c> implementation. Wire that via
    /// <c>services.AddSingleton&lt;IBls, NativeBls&gt;()</c> before calling
    /// <c>AddMainnetChainServer</c>.</para>
    /// </summary>
    internal sealed class NoopBls : IBls
    {
        public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;
        public byte[] AggregateSignatures(byte[][] signatures) => Array.Empty<byte>();
        public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => true;
        public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey)
            => (Array.Empty<byte>(), Array.Empty<byte>());
    }
}
