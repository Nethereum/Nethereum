namespace Nethereum.CoreChain.DataAvailability
{
    public enum DaMode { None, Calldata, Federated, Public, External }

    public enum ProofCarrierMode { Inline, Blob, BlobExternal }

    public enum StateModel : byte
    {
        MptKeccak = 0x00,
        BinaryPoseidon = 0x01,
        BinarySha256 = 0x02
    }

    public enum AnchorKind : byte { Block = 0, Batch = 1 }

    public enum AnchorPayloadSectionType : ushort
    {
        StateRoot = 0x0001,
        TxRoot = 0x0002,
        ReceiptRoot = 0x0003,
        InlineProof = 0x0004,
        InlineDa = 0x0005,
        ProofCommitment = 0x0006,
        DaCommitment = 0x0007,
        PreviousValidatedPointer = 0x0008,
        MessageRoot = 0x0009,
        CompressedCalldata = 0x000A
    }

    public enum DaPayloadKind { Block, Batch, WitnessBundle, StateDiff }
}
