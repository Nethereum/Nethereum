namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Per-fork block-header codec. Each fork registers an implementation
    /// as a required field on <c>HardforkSpec</c>; callers that do not know
    /// their fork reach these through <c>BlockHeaderEncoder</c>.
    ///
    /// <para>Field schedule by fork:</para>
    /// <list type="bullet">
    ///   <item>Frontier &#x2026; London-1 &#x2014; 15 fields (yellow paper).</item>
    ///   <item>London (EIP-1559) &#x2014; +<c>baseFee</c> = 16.</item>
    ///   <item>Shanghai (EIP-4895) &#x2014; +<c>withdrawalsRoot</c> = 17.</item>
    ///   <item>Cancun (EIP-4844, EIP-4788) &#x2014; +<c>blobGasUsed</c>,
    ///         <c>excessBlobGas</c>, <c>parentBeaconBlockRoot</c> = 20.</item>
    ///   <item>Prague (EIP-7685) &#x2014; +<c>requestsHash</c> = 21.</item>
    ///   <item>Amsterdam (EIP-7928, EIP-7843) &#x2014; +<c>blockAccessListHash</c>,
    ///         <c>slotNumber</c> = 23.</item>
    /// </list>
    ///
    /// <para>Each fork's codec emits and reads EXACTLY its declared field
    /// count. No nullable-cascade. No element-count guessing. AppChains can
    /// pick a codec whose field set diverges from mainnet's schedule.</para>
    ///
    /// <para>Which optional fields a header carries is therefore the codec's
    /// answer, not a fork ordering read off the list above: the
    /// <c>Carries…</c> questions are asked of the codec so a codec off the
    /// mainnet schedule still answers for itself. They enumerate every
    /// optional group, first to last, so a header can be brought to a fork's
    /// shape without any caller knowing the schedule.</para>
    /// </summary>
    public interface IBlockHeaderCodec
    {
        byte[] Encode(BlockHeader header);

        BlockHeader Decode(byte[] rawBytes);

        /// <summary>Whether this fork's header carries <c>baseFee</c> (EIP-1559).</summary>
        bool CarriesBaseFee { get; }

        /// <summary>Whether this fork's header carries <c>withdrawalsRoot</c> (EIP-4895).</summary>
        bool CarriesWithdrawalsRoot { get; }

        /// <summary>
        /// Whether this fork's header carries <c>blobGasUsed</c> and
        /// <c>excessBlobGas</c> (EIP-4844) together with
        /// <c>parentBeaconBlockRoot</c> (EIP-4788). One question for the three,
        /// because one codec introduced them together and no codec emits a
        /// subset.
        /// </summary>
        bool CarriesBlobFieldsAndBeaconRoot { get; }

        /// <summary>Whether this fork's header carries <c>requestsHash</c> (EIP-7685).</summary>
        bool CarriesRequestsHash { get; }

        /// <summary>
        /// Whether this fork's header commits to an EIP-7928 block access list
        /// and the EIP-7843 <c>slotNumber</c> beside it — the pair is emitted
        /// together or not at all.
        /// </summary>
        bool CarriesBlockAccessList { get; }

        string ShapeName { get; }
    }
}
