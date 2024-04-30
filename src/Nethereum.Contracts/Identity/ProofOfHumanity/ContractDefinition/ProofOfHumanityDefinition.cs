using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Contracts.Identity.ProofOfHumanity.ContractDefinition
{

    public partial class IsRegisteredFunction : IsRegisteredFunctionBase { }

    [Function("isRegistered", "bool")]
    public class IsRegisteredFunctionBase : FunctionMessage
    {
        [Parameter("address", "_submissionID", 1)]
        public virtual string SubmissionID { get; set; }
    }

    public partial class IsRegisteredOutputDTO : IsRegisteredOutputDTOBase { }

    [FunctionOutput]
    public class IsRegisteredOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("bool", "", 1)]
        public virtual bool IsRegistered { get; set; }
    }

    public partial class AddSubmissionEventDTO : AddSubmissionEventDTOBase { }

    [Event("AddSubmission")]
    public class AddSubmissionEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_requestID", 2, false)]
        public virtual BigInteger RequestID { get; set; }
    }

    public partial class AppealContributionEventDTO : AppealContributionEventDTOBase { }

    [Event("AppealContribution")]
    public class AppealContributionEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_challengeID", 2, true)]
        public virtual BigInteger ChallengeID { get; set; }
        [Parameter("uint8", "_party", 3, false)]
        public virtual byte Party { get; set; }
        [Parameter("address", "_contributor", 4, true)]
        public virtual string Contributor { get; set; }
        [Parameter("uint256", "_amount", 5, false)]
        public virtual BigInteger Amount { get; set; }
    }

    public partial class ArbitratorCompleteEventDTO : ArbitratorCompleteEventDTOBase { }

    [Event("ArbitratorComplete")]
    public class ArbitratorCompleteEventDTOBase : IEventDTO
    {
        [Parameter("address", "_arbitrator", 1, false)]
        public virtual string Arbitrator { get; set; }
        [Parameter("address", "_governor", 2, true)]
        public virtual string Governor { get; set; }
        [Parameter("uint256", "_submissionBaseDeposit", 3, false)]
        public virtual BigInteger SubmissionBaseDeposit { get; set; }
        [Parameter("uint256", "_submissionDuration", 4, false)]
        public virtual BigInteger SubmissionDuration { get; set; }
        [Parameter("uint256", "_challengePeriodDuration", 5, false)]
        public virtual BigInteger ChallengePeriodDuration { get; set; }
        [Parameter("uint256", "_requiredNumberOfVouches", 6, false)]
        public virtual BigInteger RequiredNumberOfVouches { get; set; }
        [Parameter("uint256", "_sharedStakeMultiplier", 7, false)]
        public virtual BigInteger SharedStakeMultiplier { get; set; }
        [Parameter("uint256", "_winnerStakeMultiplier", 8, false)]
        public virtual BigInteger WinnerStakeMultiplier { get; set; }
        [Parameter("uint256", "_loserStakeMultiplier", 9, false)]
        public virtual BigInteger LoserStakeMultiplier { get; set; }
    }

    public partial class ChallengeResolvedEventDTO : ChallengeResolvedEventDTOBase { }

    [Event("ChallengeResolved")]
    public class ChallengeResolvedEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_requestID", 2, true)]
        public virtual BigInteger RequestID { get; set; }
        [Parameter("uint256", "_challengeID", 3, false)]
        public virtual BigInteger ChallengeID { get; set; }
    }

    public partial class DisputeEventDTO : DisputeEventDTOBase { }

    [Event("Dispute")]
    public class DisputeEventDTOBase : IEventDTO
    {
        [Parameter("address", "_arbitrator", 1, true)]
        public virtual string Arbitrator { get; set; }
        [Parameter("uint256", "_disputeID", 2, true)]
        public virtual BigInteger DisputeID { get; set; }
        [Parameter("uint256", "_metaEvidenceID", 3, false)]
        public virtual BigInteger MetaEvidenceID { get; set; }
        [Parameter("uint256", "_evidenceGroupID", 4, false)]
        public virtual BigInteger EvidenceGroupID { get; set; }
    }

    public partial class EvidenceEventDTO : EvidenceEventDTOBase { }

    [Event("Evidence")]
    public class EvidenceEventDTOBase : IEventDTO
    {
        [Parameter("address", "_arbitrator", 1, true)]
        public virtual string Arbitrator { get; set; }
        [Parameter("uint256", "_evidenceGroupID", 2, true)]
        public virtual BigInteger EvidenceGroupID { get; set; }
        [Parameter("address", "_party", 3, true)]
        public virtual string Party { get; set; }
        [Parameter("string", "_evidence", 4, false)]
        public virtual string Evidence { get; set; }
    }

    public partial class HasPaidAppealFeeEventDTO : HasPaidAppealFeeEventDTOBase { }

    [Event("HasPaidAppealFee")]
    public class HasPaidAppealFeeEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_challengeID", 2, true)]
        public virtual BigInteger ChallengeID { get; set; }
        [Parameter("uint8", "_side", 3, false)]
        public virtual byte Side { get; set; }
    }

    public partial class MetaEvidenceEventDTO : MetaEvidenceEventDTOBase { }

    [Event("MetaEvidence")]
    public class MetaEvidenceEventDTOBase : IEventDTO
    {
        [Parameter("uint256", "_metaEvidenceID", 1, true)]
        public virtual BigInteger MetaEvidenceID { get; set; }
        [Parameter("string", "_evidence", 2, false)]
        public virtual string Evidence { get; set; }
    }

    public partial class ReapplySubmissionEventDTO : ReapplySubmissionEventDTOBase { }

    [Event("ReapplySubmission")]
    public class ReapplySubmissionEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_requestID", 2, false)]
        public virtual BigInteger RequestID { get; set; }
    }

    public partial class RemoveSubmissionEventDTO : RemoveSubmissionEventDTOBase { }

    [Event("RemoveSubmission")]
    public class RemoveSubmissionEventDTOBase : IEventDTO
    {
        [Parameter("address", "_requester", 1, true)]
        public virtual string Requester { get; set; }
        [Parameter("address", "_submissionID", 2, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_requestID", 3, false)]
        public virtual BigInteger RequestID { get; set; }
    }

    public partial class RulingEventDTO : RulingEventDTOBase { }

    [Event("Ruling")]
    public class RulingEventDTOBase : IEventDTO
    {
        [Parameter("address", "_arbitrator", 1, true)]
        public virtual string Arbitrator { get; set; }
        [Parameter("uint256", "_disputeID", 2, true)]
        public virtual BigInteger DisputeID { get; set; }
        [Parameter("uint256", "_ruling", 3, false)]
        public virtual BigInteger Ruling { get; set; }
    }

    public partial class SubmissionChallengedEventDTO : SubmissionChallengedEventDTOBase { }

    [Event("SubmissionChallenged")]
    public class SubmissionChallengedEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("uint256", "_requestID", 2, true)]
        public virtual BigInteger RequestID { get; set; }
        [Parameter("uint256", "_challengeID", 3, false)]
        public virtual BigInteger ChallengeID { get; set; }
    }

    public partial class VouchAddedEventDTO : VouchAddedEventDTOBase { }

    [Event("VouchAdded")]
    public class VouchAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("address", "_voucher", 2, true)]
        public virtual string Voucher { get; set; }
    }

    public partial class VouchRemovedEventDTO : VouchRemovedEventDTOBase { }

    [Event("VouchRemoved")]
    public class VouchRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "_submissionID", 1, true)]
        public virtual string SubmissionID { get; set; }
        [Parameter("address", "_voucher", 2, true)]
        public virtual string Voucher { get; set; }
    }


}
