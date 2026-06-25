namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Per-fork transaction decoder. Each fork registers an implementation
    /// as a required field on <c>HardforkSpec</c>. Gates which tx types
    /// (EIP-2718 envelope byte) are accepted at this fork — rejects e.g.
    /// an EIP-1559 envelope at Frontier or a 7702 envelope at Cancun.
    ///
    /// <para>Encoding is type-specific (not fork-specific) — the canonical
    /// form is already produced by each tx subtype's own encoder. Signing
    /// and re-encoding for the trie / wire use the type-specific encoder
    /// directly; no fork-aware codec is needed on the encode side.</para>
    ///
    /// <para>Decoding discriminates by first byte: <c>&gt;= 0xc0</c> →
    /// legacy RLP list; <c>0x01..0x04</c> → typed envelope. Each fork
    /// accepts a specific subset:</para>
    /// <list type="bullet">
    ///   <item>Frontier..Berlin-1: legacy only.</item>
    ///   <item>Berlin: + 0x01 (EIP-2930 access list).</item>
    ///   <item>London: + 0x02 (EIP-1559 dynamic fee).</item>
    ///   <item>Cancun: + 0x03 (EIP-4844 blob).</item>
    ///   <item>Prague: + 0x04 (EIP-7702 set-code).</item>
    /// </list>
    /// </summary>
    public interface ITransactionDecoder
    {
        /// <summary>
        /// Decode the canonical tx bytes. Throws or returns null for type
        /// bytes not gated by this fork (implementation-defined; consumer
        /// of the spec decides which signal it wants).
        /// </summary>
        ISignedTransaction Decode(byte[] rawBytes);
    }
}
