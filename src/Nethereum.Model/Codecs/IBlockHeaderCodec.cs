namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Per-fork block-header codec. Each fork registers an implementation
    /// as a required field on <c>HardforkSpec</c>. Replaces the global
    /// <c>BlockHeaderEncoder.Current</c> whose null-field gates + element-count
    /// decode-by-length break on every new optional header field.
    ///
    /// <para>Field schedule by fork:</para>
    /// <list type="bullet">
    ///   <item>Frontier &#x2026; London-1 &#x2014; 15 fields (yellow paper).</item>
    ///   <item>London (EIP-1559) &#x2014; +<c>baseFee</c> = 16.</item>
    ///   <item>Shanghai (EIP-4895) &#x2014; +<c>withdrawalsRoot</c> = 17.</item>
    ///   <item>Cancun (EIP-4844, EIP-4788) &#x2014; +<c>blobGasUsed</c>,
    ///         <c>excessBlobGas</c>, <c>parentBeaconBlockRoot</c> = 20.</item>
    ///   <item>Prague (EIP-7685) &#x2014; +<c>requestsHash</c> = 21.</item>
    ///   <item>Glamsterdam (EIP-7928) &#x2014; +<c>blockAccessListHash</c> = 22.</item>
    /// </list>
    ///
    /// <para>Each fork's codec emits and reads EXACTLY its declared field
    /// count. No nullable-cascade. No element-count guessing. AppChains can
    /// pick a codec whose field set diverges from mainnet's schedule.</para>
    /// </summary>
    public interface IBlockHeaderCodec
    {
        /// <summary>Canonical RLP encoding. Used for block-hash computation, peer-serving, and storage.</summary>
        byte[] Encode(BlockHeader header);

        /// <summary>Decode wire RLP. Expects exactly the field count this fork emits.</summary>
        BlockHeader Decode(byte[] rawBytes);
    }
}
