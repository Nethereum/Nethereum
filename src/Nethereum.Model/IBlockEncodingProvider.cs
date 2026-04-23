namespace Nethereum.Model
{
    /// <summary>
    /// Codec strategy for block-level objects (header / receipt / account /
    /// log / withdrawal / transaction). Two implementations:
    /// <see cref="RlpBlockEncodingProvider"/> (mainnet + binary-trie-with-RLP)
    /// and <see cref="Nethereum.Model.SSZ.SszBlockEncodingProvider"/>
    /// (EIP-7807 full-SSZ stack). An AppChain picks one at genesis via
    /// <see cref="Nethereum.AppChain.AppChainFork"/>.
    ///
    /// All methods are symmetric — <c>Decode(Encode(x))</c> round-trips for
    /// both implementations. <see cref="EncodeAccount"/> / <see cref="DecodeAccount"/>
    /// are N/A in the SSZ path (EIP-7864 binary-trie chains pack accounts as
    /// 32-byte Basic Data Leaves, not SSZ-serialised Account records) and will
    /// throw <c>NotImplementedException</c>; callers on that path should go
    /// through <c>Nethereum.Merkle.Binary.Keys</c> directly.
    /// </summary>
    public interface IBlockEncodingProvider
    {
        byte[] EncodeReceipt(Receipt receipt);
        byte[] EncodeBlockHeader(BlockHeader header);
        byte[] EncodeAccount(Account account);
        byte[] EncodeLog(Log log);
        byte[] EncodeWithdrawal(ulong index, ulong validatorIndex, byte[] address, ulong amountInGwei);

        /// <summary>
        /// Produces the wire-format bytes for a signed transaction. For
        /// <see cref="RlpBlockEncodingProvider"/> this is the concrete
        /// transaction's <c>GetRLPEncoded()</c> output (typed-transaction
        /// envelope for 2930 / 1559 / 4844 / 7702; RLP list for legacy).
        /// For <see cref="Nethereum.Model.SSZ.SszBlockEncodingProvider"/> it is
        /// the EIP-6404 CompatibleUnion `[selector][payload][signature]`.
        /// </summary>
        byte[] EncodeTransaction(ISignedTransaction transaction);

        Receipt DecodeReceipt(byte[] data);
        BlockHeader DecodeBlockHeader(byte[] data);
        Account DecodeAccount(byte[] data);
        Log DecodeLog(byte[] data);

        /// <summary>
        /// Reconstructs a signed transaction from bytes produced by
        /// <see cref="EncodeTransaction"/>. Symmetric with encode — RLP goes
        /// through <c>TransactionFactory.CreateTransaction</c>; SSZ reads the
        /// EIP-6404 CompatibleUnion selector and dispatches to the matching
        /// payload decoder.
        /// </summary>
        ISignedTransaction DecodeTransaction(byte[] data);
    }
}
