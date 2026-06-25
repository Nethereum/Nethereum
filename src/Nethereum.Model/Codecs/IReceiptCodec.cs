namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Per-fork receipt codec. Each fork registers an implementation as a
    /// required field on <c>HardforkSpec</c>; encode/decode call sites
    /// resolve the codec through the active fork's spec rather than the
    /// global <see cref="ReceiptEncoder.Current"/>.
    ///
    /// <para>The receipt RLP shape differs by fork:</para>
    /// <list type="bullet">
    ///   <item>Frontier &#x2026; Tangerine Whistle &#x2014;
    ///         <c>RLP([postState(32), cumGas, bloom, logs])</c></item>
    ///   <item>Byzantium onward (EIP-658) &#x2014;
    ///         <c>RLP([status, cumGas, bloom, logs])</c></item>
    ///   <item>Berlin onward (EIP-2718 typed envelope) &#x2014;
    ///         <c>typeByte || RLP([status, cumGas, bloom, logs])</c> for typed receipts</item>
    /// </list>
    ///
    /// <para>The shape returned by <see cref="Encode"/> is the canonical
    /// form hashed in the receipts-trie. Wire-only variants (e.g. eth/69
    /// EIP-7642 bloom-stripped) live on separate codecs reserved for the
    /// transport layer; the trie always uses this codec.</para>
    /// </summary>
    public interface IReceiptCodec
    {
        /// <summary>Canonical fork-specific RLP encoding for trie / storage / persistence.</summary>
        byte[] Encode(Receipt receipt);

        /// <summary>Inverse of <see cref="Encode"/>. Sets the right fields on the returned <see cref="Receipt"/> based on what's in the wire bytes.</summary>
        Receipt Decode(byte[] rawBytes);
    }
}
