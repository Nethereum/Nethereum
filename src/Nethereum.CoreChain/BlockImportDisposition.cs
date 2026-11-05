using Nethereum.Documentation;
using System.Collections.Generic;

namespace Nethereum.CoreChain
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "BlockImportDisposition - where an imported block landed")]
    public enum BlockImportDisposition
    {
        ExtendedCanonical,
        StoredAsSide,
        AlreadyCanonical,
        AlreadySide,
        Refused,
        UnknownParent
    }

    public sealed record ChainReorganisation(
        ulong CommonAncestor,
        IReadOnlyList<byte[]> DroppedBlockHashes,
        IReadOnlyList<byte[]> AddedBlockHashes,
        byte[]? FailedBlockHash,
        byte[]? FailedBlockParentHash,
        string? Detail,
        IReadOnlyList<Nethereum.Model.ISignedTransaction>? DroppedTransactions = null);
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "SetCanonicalStatus - the outcome of making a stored branch canonical")]
    public enum SetCanonicalStatus
    {
        NoOp,
        Reorganised,
        BranchInvalid,
        AncestryUnavailable,
        RewindUnavailable,
        TooDeepReorg
    }

    public sealed record SetCanonicalResult(
        SetCanonicalStatus Status,
        ChainReorganisation? Reorganisation,
        string? Detail,
        IReadOnlyList<Nethereum.Model.ISignedTransaction>? DroppedTransactions = null);
}
