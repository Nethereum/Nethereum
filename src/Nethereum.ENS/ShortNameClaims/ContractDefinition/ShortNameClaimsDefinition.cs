using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;

namespace Nethereum.ENS.ShortNameClaims.ContractDefinition
{
    public partial class ShortNameClaimsDeployment : ShortNameClaimsDeploymentBase
    {
        public ShortNameClaimsDeployment() : base(BYTECODE) { }
        public ShortNameClaimsDeployment(string byteCode) : base(byteCode) { }
    }

    public class ShortNameClaimsDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x";
        public ShortNameClaimsDeploymentBase() : base(BYTECODE) { }
        public ShortNameClaimsDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_priceOracle", 1)]
        public virtual string PriceOracle { get; set; }
        [Parameter("address", "_registrar", 2)]
        public virtual string Registrar { get; set; }
        [Parameter("address", "_ratifier", 3)]
        public virtual string Ratifier { get; set; }
    }

    public partial class RemoveOwnerFunction : RemoveOwnerFunctionBase { }

    [Function("removeOwner")]
    public class RemoveOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class PriceOracleFunction : PriceOracleFunctionBase { }

    [Function("priceOracle", "address")]
    public class PriceOracleFunctionBase : FunctionMessage
    {

    }

    public partial class RegistrarFunction : RegistrarFunctionBase { }

    [Function("registrar", "address")]
    public class RegistrarFunctionBase : FunctionMessage
    {

    }

    public partial class RemoveRatifierFunction : RemoveRatifierFunctionBase { }

    [Function("removeRatifier")]
    public class RemoveRatifierFunctionBase : FunctionMessage
    {
        [Parameter("address", "ratifier", 1)]
        public virtual string Ratifier { get; set; }
    }

    public partial class PendingClaimsFunction : PendingClaimsFunctionBase { }

    [Function("pendingClaims", "uint256")]
    public class PendingClaimsFunctionBase : FunctionMessage
    {

    }

    public partial class AddRatifierFunction : AddRatifierFunctionBase { }

    [Function("addRatifier")]
    public class AddRatifierFunctionBase : FunctionMessage
    {
        [Parameter("address", "ratifier", 1)]
        public virtual string Ratifier { get; set; }
    }

    public partial class SubmitPrefixClaimFunction : SubmitPrefixClaimFunctionBase { }

    [Function("submitPrefixClaim")]
    public class SubmitPrefixClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "name", 1)]
        public virtual byte[] Name { get; set; }
        [Parameter("address", "claimant", 2)]
        public virtual string Claimant { get; set; }
        [Parameter("string", "email", 3)]
        public virtual string Email { get; set; }
    }

    public partial class UnresolvedClaimsFunction : UnresolvedClaimsFunctionBase { }

    [Function("unresolvedClaims", "uint256")]
    public class UnresolvedClaimsFunctionBase : FunctionMessage
    {

    }

    public partial class GetClaimCostFunction : GetClaimCostFunctionBase { }

    [Function("getClaimCost", "uint256")]
    public class GetClaimCostFunctionBase : FunctionMessage
    {
        [Parameter("string", "claimed", 1)]
        public virtual string Claimed { get; set; }
    }

    public partial class AddOwnerFunction : AddOwnerFunctionBase { }

    [Function("addOwner")]
    public class AddOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class WithdrawClaimFunction : WithdrawClaimFunctionBase { }

    [Function("withdrawClaim")]
    public class WithdrawClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "claimId", 1)]
        public virtual byte[] ClaimId { get; set; }
    }

    public partial class DestroyFunction : DestroyFunctionBase { }

    [Function("destroy")]
    public class DestroyFunctionBase : FunctionMessage
    {

    }

    public partial class RatifyClaimsFunction : RatifyClaimsFunctionBase { }

    [Function("ratifyClaims")]
    public class RatifyClaimsFunctionBase : FunctionMessage
    {

    }

    public partial class ComputeClaimIdFunction : ComputeClaimIdFunctionBase { }

    [Function("computeClaimId", "bytes32")]
    public class ComputeClaimIdFunctionBase : FunctionMessage
    {
        [Parameter("string", "claimed", 1)]
        public virtual string Claimed { get; set; }
        [Parameter("bytes", "dnsname", 2)]
        public virtual byte[] Dnsname { get; set; }
        [Parameter("address", "claimant", 3)]
        public virtual string Claimant { get; set; }
        [Parameter("string", "email", 4)]
        public virtual string Email { get; set; }
    }

    public partial class SetClaimStatusFunction : SetClaimStatusFunctionBase { }

    [Function("setClaimStatus")]
    public class SetClaimStatusFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "claimId", 1)]
        public virtual byte[] ClaimId { get; set; }
        [Parameter("bool", "approved", 2)]
        public virtual bool Approved { get; set; }
    }

    public partial class SubmitExactClaimFunction : SubmitExactClaimFunctionBase { }

    [Function("submitExactClaim")]
    public class SubmitExactClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "name", 1)]
        public virtual byte[] Name { get; set; }
        [Parameter("address", "claimant", 2)]
        public virtual string Claimant { get; set; }
        [Parameter("string", "email", 3)]
        public virtual string Email { get; set; }
    }

    public partial class PhaseFunction : PhaseFunctionBase { }

    [Function("phase", "uint8")]
    public class PhaseFunctionBase : FunctionMessage
    {

    }

    public partial class ResolveClaimFunction : ResolveClaimFunctionBase { }

    [Function("resolveClaim")]
    public class ResolveClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "claimId", 1)]
        public virtual byte[] ClaimId { get; set; }
    }

    public partial class SetClaimStatusesFunction : SetClaimStatusesFunctionBase { }

    [Function("setClaimStatuses")]
    public class SetClaimStatusesFunctionBase : FunctionMessage
    {
        [Parameter("bytes32[]", "approved", 1)]
        public virtual List<byte[]> Approved { get; set; }
        [Parameter("bytes32[]", "declined", 2)]
        public virtual List<byte[]> Declined { get; set; }
    }

    public partial class CloseClaimsFunction : CloseClaimsFunctionBase { }

    [Function("closeClaims")]
    public class CloseClaimsFunctionBase : FunctionMessage
    {

    }

    public partial class ResolveClaimsFunction : ResolveClaimsFunctionBase { }

    [Function("resolveClaims")]
    public class ResolveClaimsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32[]", "claimIds", 1)]
        public virtual List<byte[]> ClaimIds { get; set; }
    }

    public partial class REGISTRATION_PERIODFunction : REGISTRATION_PERIODFunctionBase { }

    [Function("REGISTRATION_PERIOD", "uint256")]
    public class REGISTRATION_PERIODFunctionBase : FunctionMessage
    {

    }

    public partial class ClaimsFunction : ClaimsFunctionBase { }

    [Function("claims", typeof(ClaimsOutputDTO))]
    public class ClaimsFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SubmitCombinedClaimFunction : SubmitCombinedClaimFunctionBase { }

    [Function("submitCombinedClaim")]
    public class SubmitCombinedClaimFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "name", 1)]
        public virtual byte[] Name { get; set; }
        [Parameter("address", "claimant", 2)]
        public virtual string Claimant { get; set; }
        [Parameter("string", "email", 3)]
        public virtual string Email { get; set; }
    }

    public partial class ClaimSubmittedEventDTO : ClaimSubmittedEventDTOBase { }

    [Event("ClaimSubmitted")]
    public class ClaimSubmittedEventDTOBase : IEventDTO
    {
        [Parameter("string", "claimed", 1, false )]
        public virtual string Claimed { get; set; }
        [Parameter("bytes", "dnsname", 2, false )]
        public virtual byte[] Dnsname { get; set; }
        [Parameter("uint256", "paid", 3, false )]
        public virtual BigInteger Paid { get; set; }
        [Parameter("address", "claimant", 4, false )]
        public virtual string Claimant { get; set; }
        [Parameter("string", "email", 5, false )]
        public virtual string Email { get; set; }
    }

    public partial class ClaimStatusChangedEventDTO : ClaimStatusChangedEventDTOBase { }

    [Event("ClaimStatusChanged")]
    public class ClaimStatusChangedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "claimId", 1, true )]
        public virtual byte[] ClaimId { get; set; }
        [Parameter("uint8", "status", 2, false )]
        public virtual byte Status { get; set; }
    }



    public partial class PriceOracleOutputDTO : PriceOracleOutputDTOBase { }

    [FunctionOutput]
    public class PriceOracleOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RegistrarOutputDTO : RegistrarOutputDTOBase { }

    [FunctionOutput]
    public class RegistrarOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }



    public partial class PendingClaimsOutputDTO : PendingClaimsOutputDTOBase { }

    [FunctionOutput]
    public class PendingClaimsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class UnresolvedClaimsOutputDTO : UnresolvedClaimsOutputDTOBase { }

    [FunctionOutput]
    public class UnresolvedClaimsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetClaimCostOutputDTO : GetClaimCostOutputDTOBase { }

    [FunctionOutput]
    public class GetClaimCostOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }









    public partial class ComputeClaimIdOutputDTO : ComputeClaimIdOutputDTOBase { }

    [FunctionOutput]
    public class ComputeClaimIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }





    public partial class PhaseOutputDTO : PhaseOutputDTOBase { }

    [FunctionOutput]
    public class PhaseOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }









    public partial class REGISTRATION_PERIODOutputDTO : REGISTRATION_PERIODOutputDTOBase { }

    [FunctionOutput]
    public class REGISTRATION_PERIODOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class ClaimsOutputDTO : ClaimsOutputDTOBase { }

    [FunctionOutput]
    public class ClaimsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "labelHash", 1)]
        public virtual byte[] LabelHash { get; set; }
        [Parameter("address", "claimant", 2)]
        public virtual string Claimant { get; set; }
        [Parameter("uint256", "paid", 3)]
        public virtual BigInteger Paid { get; set; }
        [Parameter("uint8", "status", 4)]
        public virtual byte Status { get; set; }
    }


}
