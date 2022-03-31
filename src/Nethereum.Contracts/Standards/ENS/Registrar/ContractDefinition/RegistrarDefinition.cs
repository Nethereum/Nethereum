using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.Registrar.ContractDefinition
{
    
    
    public partial class ReleaseDeedFunction:ReleaseDeedFunctionBase{}

    [Function("releaseDeed")]
    public class ReleaseDeedFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class GetAllowedTimeFunction:GetAllowedTimeFunctionBase{}

    [Function("getAllowedTime", "uint256")]
    public class GetAllowedTimeFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class InvalidateNameFunction:InvalidateNameFunctionBase{}

    [Function("invalidateName")]
    public class InvalidateNameFunctionBase:FunctionMessage
    {
        [Parameter("string", "unhashedName", 1)]
        public virtual string UnhashedName {get; set;}
    }    
    
    public partial class ShaBidFunction:ShaBidFunctionBase{}

    [Function("shaBid", "bytes32")]
    public class ShaBidFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "hash", 1)]
        public virtual byte[] Hash {get; set;}
        [Parameter("address", "owner", 2)]
        public virtual string Owner {get; set;}
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value {get; set;}
        [Parameter("bytes32", "salt", 4)]
        public virtual byte[] Salt {get; set;}
    }    
    
    public partial class CancelBidFunction:CancelBidFunctionBase{}

    [Function("cancelBid")]
    public class CancelBidFunctionBase:FunctionMessage
    {
        [Parameter("address", "bidder", 1)]
        public virtual string Bidder {get; set;}
        [Parameter("bytes32", "seal", 2)]
        public virtual byte[] Seal {get; set;}
    }    
    
    public partial class EntriesFunction:EntriesFunctionBase{}

    [Function("entries", typeof(EntriesOutputDTO))]
    public class EntriesFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class EnsFunction:EnsFunctionBase{}

    [Function("ens", "address")]
    public class EnsFunctionBase:FunctionMessage
    {

    }    
    
    public partial class UnsealBidFunction:UnsealBidFunctionBase{}

    [Function("unsealBid")]
    public class UnsealBidFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
        [Parameter("uint256", "_value", 2)]
        public virtual BigInteger Value {get; set;}
        [Parameter("bytes32", "_salt", 3)]
        public virtual byte[] Salt {get; set;}
    }    
    
    public partial class TransferRegistrarsFunction:TransferRegistrarsFunctionBase{}

    [Function("transferRegistrars")]
    public class TransferRegistrarsFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class SealedBidsFunction:SealedBidsFunctionBase{}

    [Function("sealedBids", "address")]
    public class SealedBidsFunctionBase:FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 {get; set;}
    }    
    
    public partial class StateFunction:StateFunctionBase{}

    [Function("state", "uint8")]
    public class StateFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class TransferFunction:TransferFunctionBase{}

    [Function("transfer")]
    public class TransferFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
        [Parameter("address", "newOwner", 2)]
        public virtual string NewOwner {get; set;}
    }    
    
    public partial class IsAllowedFunction:IsAllowedFunctionBase{}

    [Function("isAllowed", "bool")]
    public class IsAllowedFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
        [Parameter("uint256", "_timestamp", 2)]
        public virtual BigInteger Timestamp {get; set;}
    }    
    
    public partial class FinalizeAuctionFunction:FinalizeAuctionFunctionBase{}

    [Function("finalizeAuction")]
    public class FinalizeAuctionFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class RegistryStartedFunction:RegistryStartedFunctionBase{}

    [Function("registryStarted", "uint256")]
    public class RegistryStartedFunctionBase:FunctionMessage
    {

    }    
    
    public partial class LaunchLengthFunction:LaunchLengthFunctionBase{}

    [Function("launchLength", "uint32")]
    public class LaunchLengthFunctionBase:FunctionMessage
    {

    }    
    
    public partial class NewBidFunction:NewBidFunctionBase{}

    [Function("newBid")]
    public class NewBidFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "sealedBid", 1)]
        public virtual byte[] SealedBid {get; set;}
    }    
    
    public partial class EraseNodeFunction:EraseNodeFunctionBase{}

    [Function("eraseNode")]
    public class EraseNodeFunctionBase:FunctionMessage
    {
        [Parameter("bytes32[]", "labels", 1)]
        public virtual List<byte[]> Labels {get; set;}
    }    
    
    public partial class StartAuctionsFunction:StartAuctionsFunctionBase{}

    [Function("startAuctions")]
    public class StartAuctionsFunctionBase:FunctionMessage
    {
        [Parameter("bytes32[]", "_hashes", 1)]
        public virtual List<byte[]> Hashes {get; set;}
    }    
    
    public partial class AcceptRegistrarTransferFunction:AcceptRegistrarTransferFunctionBase{}

    [Function("acceptRegistrarTransfer")]
    public class AcceptRegistrarTransferFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "hash", 1)]
        public virtual byte[] Hash {get; set;}
        [Parameter("address", "deed", 2)]
        public virtual string Deed {get; set;}
        [Parameter("uint256", "registrationDate", 3)]
        public virtual BigInteger RegistrationDate {get; set;}
    }    
    
    public partial class StartAuctionFunction:StartAuctionFunctionBase{}

    [Function("startAuction")]
    public class StartAuctionFunctionBase:FunctionMessage
    {
        [Parameter("bytes32", "_hash", 1)]
        public virtual byte[] Hash {get; set;}
    }    
    
    public partial class RootNodeFunction:RootNodeFunctionBase{}

    [Function("rootNode", "bytes32")]
    public class RootNodeFunctionBase:FunctionMessage
    {

    }    
    
    public partial class StartAuctionsAndBidFunction:StartAuctionsAndBidFunctionBase{}

    [Function("startAuctionsAndBid")]
    public class StartAuctionsAndBidFunctionBase:FunctionMessage
    {
        [Parameter("bytes32[]", "hashes", 1)]
        public virtual List<byte[]> Hashes {get; set;}
        [Parameter("bytes32", "sealedBid", 2)]
        public virtual byte[] SealedBid {get; set;}
    }    
    
    public partial class AuctionStartedEventDTO:AuctionStartedEventDTOBase{}

    [Event("AuctionStarted")]
    public class AuctionStartedEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "hash", 1, true )]
        public virtual byte[] Hash {get; set;}
        [Parameter("uint256", "registrationDate", 2, false )]
        public virtual BigInteger RegistrationDate {get; set;}
    }    
    
    public partial class NewBidEventDTO:NewBidEventDTOBase{}

    [Event("NewBid")]
    public class NewBidEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "hash", 1, true )]
        public virtual byte[] Hash {get; set;}
        [Parameter("address", "bidder", 2, true )]
        public virtual string Bidder {get; set;}
        [Parameter("uint256", "deposit", 3, false )]
        public virtual BigInteger Deposit {get; set;}
    }    
    
    public partial class BidRevealedEventDTO:BidRevealedEventDTOBase{}

    [Event("BidRevealed")]
    public class BidRevealedEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "hash", 1, true )]
        public virtual byte[] Hash {get; set;}
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner {get; set;}
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value {get; set;}
        [Parameter("uint8", "status", 4, false )]
        public virtual byte Status {get; set;}
    }    
    
    public partial class HashRegisteredEventDTO:HashRegisteredEventDTOBase{}

    [Event("HashRegistered")]
    public class HashRegisteredEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "hash", 1, true )]
        public virtual byte[] Hash {get; set;}
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner {get; set;}
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value {get; set;}
        [Parameter("uint256", "registrationDate", 4, false )]
        public virtual BigInteger RegistrationDate {get; set;}
    }    
    
    public partial class HashReleasedEventDTO:HashReleasedEventDTOBase{}

    [Event("HashReleased")]
    public class HashReleasedEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "hash", 1, true )]
        public virtual byte[] Hash {get; set;}
        [Parameter("uint256", "value", 2, false )]
        public virtual BigInteger Value {get; set;}
    }    
    
    public partial class HashInvalidatedEventDTO:HashInvalidatedEventDTOBase{}

    [Event("HashInvalidated")]
    public class HashInvalidatedEventDTOBase: IEventDTO
    {
        [Parameter("bytes32", "hash", 1, true )]
        public virtual byte[] Hash {get; set;}
        [Parameter("string", "name", 2, true )]
        public virtual string Name {get; set;}
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value {get; set;}
        [Parameter("uint256", "registrationDate", 4, false )]
        public virtual BigInteger RegistrationDate {get; set;}
    }    
    
    
    
    public partial class GetAllowedTimeOutputDTO:GetAllowedTimeOutputDTOBase{}

    [FunctionOutput]
    public class GetAllowedTimeOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 {get; set;}
    }    
    
    
    
    public partial class ShaBidOutputDTO:ShaBidOutputDTOBase{}

    [FunctionOutput]
    public class ShaBidOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 {get; set;}
    }    
    
    
    
    public partial class EntriesOutputDTO:EntriesOutputDTOBase{}

    [FunctionOutput]
    public class EntriesOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 {get; set;}
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 {get; set;}
        [Parameter("uint256", "", 3)]
        public virtual BigInteger ReturnValue3 {get; set;}
        [Parameter("uint256", "", 4)]
        public virtual BigInteger ReturnValue4 {get; set;}
        [Parameter("uint256", "", 5)]
        public virtual BigInteger ReturnValue5 {get; set;}
    }    
    
    public partial class EnsOutputDTO:EnsOutputDTOBase{}

    [FunctionOutput]
    public class EnsOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }    
    
    
    
    
    
    public partial class SealedBidsOutputDTO:SealedBidsOutputDTOBase{}

    [FunctionOutput]
    public class SealedBidsOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 {get; set;}
    }    
    
    public partial class StateOutputDTO:StateOutputDTOBase{}

    [FunctionOutput]
    public class StateOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 {get; set;}
    }    
    
    
    
    public partial class IsAllowedOutputDTO:IsAllowedOutputDTOBase{}

    [FunctionOutput]
    public class IsAllowedOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("bool", "allowed", 1)]
        public virtual bool Allowed {get; set;}
    }    
    
    
    
    public partial class RegistryStartedOutputDTO:RegistryStartedOutputDTOBase{}

    [FunctionOutput]
    public class RegistryStartedOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 {get; set;}
    }    
    
    public partial class LaunchLengthOutputDTO:LaunchLengthOutputDTOBase{}

    [FunctionOutput]
    public class LaunchLengthOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("uint32", "", 1)]
        public virtual uint ReturnValue1 {get; set;}
    }    
    
    
    
    
    
    
    
    
    
    
    
    public partial class RootNodeOutputDTO:RootNodeOutputDTOBase{}

    [FunctionOutput]
    public class RootNodeOutputDTOBase :IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 {get; set;}
    }    
    

}
