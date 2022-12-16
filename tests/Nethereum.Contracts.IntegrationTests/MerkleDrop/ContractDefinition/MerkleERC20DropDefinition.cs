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

namespace Nethereum.Mekle.Contracts.MerkleERC20Drop.ContractDefinition
{


    public partial class MerkleERC20DropDeployment : MerkleERC20DropDeploymentBase
    {
        public MerkleERC20DropDeployment() : base(BYTECODE) { }
        public MerkleERC20DropDeployment(string byteCode) : base(byteCode) { }
    }

    public class MerkleERC20DropDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "60a06040523480156200001157600080fd5b5060405162000fc038038062000fc083398101604081905262000034916200015a565b60ff8616608052600062000049868262000289565b50600162000058858262000289565b50600580546001600160a01b0319163390811790915560009081526006602052604090208390556002929092556003556004555062000355915050565b634e487b7160e01b600052604160045260246000fd5b600082601f830112620000bd57600080fd5b81516001600160401b0380821115620000da57620000da62000095565b604051601f8301601f19908116603f0116810190828211818310171562000105576200010562000095565b816040528381526020925086838588010111156200012257600080fd5b600091505b8382101562000146578582018301518183018401529082019062000127565b600093810190920192909252949350505050565b60008060008060008060c087890312156200017457600080fd5b865160ff811681146200018657600080fd5b60208801519096506001600160401b0380821115620001a457600080fd5b620001b28a838b01620000ab565b96506040890151915080821115620001c957600080fd5b50620001d889828a01620000ab565b945050606087015192506080870151915060a087015190509295509295509295565b600181811c908216806200020f57607f821691505b6020821081036200023057634e487b7160e01b600052602260045260246000fd5b50919050565b601f8211156200028457600081815260208120601f850160051c810160208610156200025f5750805b601f850160051c820191505b8181101562000280578281556001016200026b565b5050505b505050565b81516001600160401b03811115620002a557620002a562000095565b620002bd81620002b68454620001fa565b8462000236565b602080601f831160018114620002f55760008415620002dc5750858301515b600019600386901b1c1916600185901b17855562000280565b600085815260208120601f198616915b82811015620003265788860151825594840194600190910190840162000305565b5085821015620003455787850151600019600388901b60f8161c191681555b5050505050600190811b01905550565b608051610c4f6200037160003960006101960152610c4f6000f3fe608060405234801561001057600080fd5b50600436106101215760003560e01c806375e4a82d116100ad578063b4863c0c11610071578063b4863c0c14610279578063c884ef831461028c578063cce72d84146102af578063cd4c07ac146102b8578063dd62ed3e146102c157600080fd5b806375e4a82d1461022557806395d89b4114610238578063a9059cbb14610240578063aa2945db14610253578063b0ab88031461026657600080fd5b8063313ce567116100f4578063313ce567146101915780633695a940146101ca5780633d13f874146101dd5780635025f67b146101f257806370a082311461020557600080fd5b806306fdde0314610126578063095ea7b31461014457806318160ddd1461016757806323b872dd1461017e575b600080fd5b61012e6102ec565b60405161013b9190610896565b60405180910390f35b6101576101523660046108c5565b61037a565b604051901515815260200161013b565b61017060025481565b60405190815260200161013b565b61015761018c3660046108ef565b610391565b6101b87f000000000000000000000000000000000000000000000000000000000000000081565b60405160ff909116815260200161013b565b6101706101d836600461092b565b610423565b6101f06101eb3660046109fe565b610455565b005b61012e6102003660046108c5565b610465565b610170610213366004610a55565b60066020526000908152604090205481565b6101f0610233366004610a70565b610491565b61012e6104a0565b61015761024e3660046108c5565b6104ad565b6101576102613660046109fe565b6104ba565b610157610274366004610ab7565b6104cf565b6101706102873660046108c5565b610533565b61015761029a366004610a55565b60076020526000908152604090205460ff1681565b61017060035481565b61017060045481565b6101706102cf366004610b1f565b600860209081526000928352604080842090915290825290205481565b600080546102f990610b52565b80601f016020809104026020016040519081016040528092919081815260200182805461032590610b52565b80156103725780601f1061034757610100808354040283529160200191610372565b820191906000526020600020905b81548152906001019060200180831161035557829003601f168201915b505050505081565b6000610387338484610566565b5060015b92915050565b6001600160a01b03831660009081526008602090815260408083203384529091528120541561040e576001600160a01b03841660009081526008602090815260408083203384529091529020546103e9908390610ba2565b6001600160a01b03851660009081526008602090815260408083203384529091529020555b6104198484846105c8565b5060019392505050565b600081831061043f57600082815260208490526040902061044e565b60008381526020839052604090205b9392505050565b610460838383610670565b505050565b6060828260405160200161047a929190610bb5565b604051602081830303815290604052905092915050565b61049c338383610670565b5050565b600180546102f990610b52565b60006103873384846105c8565b60006104c78484846107a9565b949350505050565b6040516bffffffffffffffffffffffff19606086811b8216602084015285901b16603482015260488101839052600090819060680160405160208183030381529060405280519060200120905061052983600454836107ed565b9695505050505050565b60008282604051602001610548929190610bb5565b60405160208183030381529060405280519060200120905092915050565b6001600160a01b0383811660008181526008602090815260408083209487168084529482529182902085905590518481527f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b92591015b60405180910390a3505050565b6001600160a01b0383166000908152600660205260409020546105ec908290610ba2565b6001600160a01b03808516600090815260066020526040808220939093559084168152205461061c908290610bd7565b6001600160a01b0380841660008181526006602052604090819020939093559151908516907fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef906105bb9085815260200190565b6001600160a01b03831660009081526007602052604090205460ff16156106de5760405162461bcd60e51b815260206004820181905260248201527f416464726573732068617320616c7265616479206265656e20636c61696d656460448201526064015b60405180910390fd5b6106e98383836107a9565b61072e5760405162461bcd60e51b815260206004820152601660248201527524b731b7b93932b1ba1036b2b935b63290383937b7b360511b60448201526064016106d5565b6001600160a01b038084166000908152600760205260409020805460ff19166001179055600554610761911684846105c8565b826001600160a01b03167f3d711a4f570ebb5a97f8779f69210b8d0e3062bf56bb7a34f90943812f17f1288360405161079c91815260200190565b60405180910390a2505050565b60008084846040516020016107bf929190610bb5565b6040516020818303038152906040528051906020012090506107e483600354836107ed565b95945050505050565b6000826107fa8584610803565b14949350505050565b600081815b8451811015610848576108348286838151811061082757610827610bea565b6020026020010151610423565b91508061084081610c00565b915050610808565b509392505050565b6000815180845260005b818110156108765760208185018101518683018201520161085a565b506000602082860101526020601f19601f83011685010191505092915050565b60208152600061044e6020830184610850565b80356001600160a01b03811681146108c057600080fd5b919050565b600080604083850312156108d857600080fd5b6108e1836108a9565b946020939093013593505050565b60008060006060848603121561090457600080fd5b61090d846108a9565b925061091b602085016108a9565b9150604084013590509250925092565b6000806040838503121561093e57600080fd5b50508035926020909101359150565b634e487b7160e01b600052604160045260246000fd5b600082601f83011261097457600080fd5b8135602067ffffffffffffffff808311156109915761099161094d565b8260051b604051601f19603f830116810181811084821117156109b6576109b661094d565b6040529384528581018301938381019250878511156109d457600080fd5b83870191505b848210156109f3578135835291830191908301906109da565b979650505050505050565b600080600060608486031215610a1357600080fd5b610a1c846108a9565b925060208401359150604084013567ffffffffffffffff811115610a3f57600080fd5b610a4b86828701610963565b9150509250925092565b600060208284031215610a6757600080fd5b61044e826108a9565b60008060408385031215610a8357600080fd5b82359150602083013567ffffffffffffffff811115610aa157600080fd5b610aad85828601610963565b9150509250929050565b60008060008060808587031215610acd57600080fd5b610ad6856108a9565b9350610ae4602086016108a9565b925060408501359150606085013567ffffffffffffffff811115610b0757600080fd5b610b1387828801610963565b91505092959194509250565b60008060408385031215610b3257600080fd5b610b3b836108a9565b9150610b49602084016108a9565b90509250929050565b600181811c90821680610b6657607f821691505b602082108103610b8657634e487b7160e01b600052602260045260246000fd5b50919050565b634e487b7160e01b600052601160045260246000fd5b8181038181111561038b5761038b610b8c565b60609290921b6bffffffffffffffffffffffff19168252601482015260340190565b8082018082111561038b5761038b610b8c565b634e487b7160e01b600052603260045260246000fd5b600060018201610c1257610c12610b8c565b506001019056fea2646970667358221220c6e49766c43d9d7661dda4714ae44c640e730824d26cf4c7f508841b2aa2fb8e64736f6c63430008110033";
        public MerkleERC20DropDeploymentBase() : base(BYTECODE) { }
        public MerkleERC20DropDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("uint8", "_decimals", 1)]
        public virtual byte Decimals { get; set; }
        [Parameter("string", "_name", 2)]
        public virtual string Name { get; set; }
        [Parameter("string", "_symbol", 3)]
        public virtual string Symbol { get; set; }
        [Parameter("uint256", "_initialSupply", 4)]
        public virtual BigInteger InitialSupply { get; set; }
        [Parameter("bytes32", "_rootMerkleDrop", 5)]
        public virtual byte[] RootMerkleDrop { get; set; }
        [Parameter("bytes32", "_rootMerklePayment", 6)]
        public virtual byte[] RootMerklePayment { get; set; }
    }

    public partial class AllowanceFunction : AllowanceFunctionBase { }

    [Function("allowance", "uint256")]
    public class AllowanceFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
    }

    public partial class ApproveFunction : ApproveFunctionBase { }

    [Function("approve", "bool")]
    public class ApproveFunctionBase : FunctionMessage
    {
        [Parameter("address", "spender", 1)]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class BalanceOfFunction : BalanceOfFunctionBase { }

    [Function("balanceOf", "uint256")]
    public class BalanceOfFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ClaimFunction : ClaimFunctionBase { }

    [Function("claim")]
    public class ClaimFunctionBase : FunctionMessage
    {
        [Parameter("address", "claimAddress", 1)]
        public virtual string ClaimAddress { get; set; }
        [Parameter("uint256", "balance", 2)]
        public virtual BigInteger Balance { get; set; }
        [Parameter("bytes32[]", "merkleProof", 3)]
        public virtual List<byte[]> MerkleProof { get; set; }
    }

    public partial class ClaimSenderFunction : ClaimSenderFunctionBase { }

    [Function("claimSender")]
    public class ClaimSenderFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "balance", 1)]
        public virtual BigInteger Balance { get; set; }
        [Parameter("bytes32[]", "merkleProof", 2)]
        public virtual List<byte[]> MerkleProof { get; set; }
    }

    public partial class ClaimedFunction : ClaimedFunctionBase { }

    [Function("claimed", "bool")]
    public class ClaimedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class ComputeEncodedPackedDropFunction : ComputeEncodedPackedDropFunctionBase { }

    [Function("computeEncodedPackedDrop", "bytes")]
    public class ComputeEncodedPackedDropFunctionBase : FunctionMessage
    {
        [Parameter("address", "claimAddress", 1)]
        public virtual string ClaimAddress { get; set; }
        [Parameter("uint256", "balance", 2)]
        public virtual BigInteger Balance { get; set; }
    }

    public partial class ComputeLeafDropFunction : ComputeLeafDropFunctionBase { }

    [Function("computeLeafDrop", "bytes32")]
    public class ComputeLeafDropFunctionBase : FunctionMessage
    {
        [Parameter("address", "claimAddress", 1)]
        public virtual string ClaimAddress { get; set; }
        [Parameter("uint256", "balance", 2)]
        public virtual BigInteger Balance { get; set; }
    }

    public partial class DecimalsFunction : DecimalsFunctionBase { }

    [Function("decimals", "uint8")]
    public class DecimalsFunctionBase : FunctionMessage
    {

    }

    public partial class HashPairFunction : HashPairFunctionBase { }

    [Function("hashPair", "bytes32")]
    public class HashPairFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "a", 1)]
        public virtual byte[] A { get; set; }
        [Parameter("bytes32", "b", 2)]
        public virtual byte[] B { get; set; }
    }

    public partial class NameFunction : NameFunctionBase { }

    [Function("name", "string")]
    public class NameFunctionBase : FunctionMessage
    {

    }

    public partial class RootMerkleDropFunction : RootMerkleDropFunctionBase { }

    [Function("rootMerkleDrop", "bytes32")]
    public class RootMerkleDropFunctionBase : FunctionMessage
    {

    }

    public partial class RootMerklePaymentFunction : RootMerklePaymentFunctionBase { }

    [Function("rootMerklePayment", "bytes32")]
    public class RootMerklePaymentFunctionBase : FunctionMessage
    {

    }

    public partial class SymbolFunction : SymbolFunctionBase { }

    [Function("symbol", "string")]
    public class SymbolFunctionBase : FunctionMessage
    {

    }

    public partial class TotalSupplyFunction : TotalSupplyFunctionBase { }

    [Function("totalSupply", "uint256")]
    public class TotalSupplyFunctionBase : FunctionMessage
    {

    }

    public partial class TransferFunction : TransferFunctionBase { }

    [Function("transfer", "bool")]
    public class TransferFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 2)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class TransferFromFunction : TransferFromFunctionBase { }

    [Function("transferFrom", "bool")]
    public class TransferFromFunctionBase : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2)]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3)]
        public virtual BigInteger Value { get; set; }
    }

    public partial class VerifyClaimFunction : VerifyClaimFunctionBase { }

    [Function("verifyClaim", "bool")]
    public class VerifyClaimFunctionBase : FunctionMessage
    {
        [Parameter("address", "claimAddress", 1)]
        public virtual string ClaimAddress { get; set; }
        [Parameter("uint256", "balance", 2)]
        public virtual BigInteger Balance { get; set; }
        [Parameter("bytes32[]", "merkleProof", 3)]
        public virtual List<byte[]> MerkleProof { get; set; }
    }

    public partial class VerifyPaymentIncludedFunction : VerifyPaymentIncludedFunctionBase { }

    [Function("verifyPaymentIncluded", "bool")]
    public class VerifyPaymentIncludedFunctionBase : FunctionMessage
    {
        [Parameter("address", "sender", 1)]
        public virtual string Sender { get; set; }
        [Parameter("address", "claimAddress", 2)]
        public virtual string ClaimAddress { get; set; }
        [Parameter("uint256", "balance", 3)]
        public virtual BigInteger Balance { get; set; }
        [Parameter("bytes32[]", "merkleProof", 4)]
        public virtual List<byte[]> MerkleProof { get; set; }
    }

    public partial class ApprovalEventDTO : ApprovalEventDTOBase { }

    [Event("Approval")]
    public class ApprovalEventDTOBase : IEventDTO
    {
        [Parameter("address", "owner", 1, true )]
        public virtual string Owner { get; set; }
        [Parameter("address", "spender", 2, true )]
        public virtual string Spender { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class ClaimedMerkleDropEventDTO : ClaimedMerkleDropEventDTOBase { }

    [Event("ClaimedMerkleDrop")]
    public class ClaimedMerkleDropEventDTOBase : IEventDTO
    {
        [Parameter("address", "receiver", 1, true )]
        public virtual string Receiver { get; set; }
        [Parameter("uint256", "value", 2, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class TransferEventDTO : TransferEventDTOBase { }

    [Event("Transfer")]
    public class TransferEventDTOBase : IEventDTO
    {
        [Parameter("address", "from", 1, true )]
        public virtual string From { get; set; }
        [Parameter("address", "to", 2, true )]
        public virtual string To { get; set; }
        [Parameter("uint256", "value", 3, false )]
        public virtual BigInteger Value { get; set; }
    }

    public partial class AllowanceOutputDTO : AllowanceOutputDTOBase { }

    [FunctionOutput]
    public class AllowanceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }



    public partial class BalanceOfOutputDTO : BalanceOfOutputDTOBase { }

    [FunctionOutput]
    public class BalanceOfOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class ClaimedOutputDTO : ClaimedOutputDTOBase { }

    [FunctionOutput]
    public class ClaimedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ComputeEncodedPackedDropOutputDTO : ComputeEncodedPackedDropOutputDTOBase { }

    [FunctionOutput]
    public class ComputeEncodedPackedDropOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ComputeLeafDropOutputDTO : ComputeLeafDropOutputDTOBase { }

    [FunctionOutput]
    public class ComputeLeafDropOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class DecimalsOutputDTO : DecimalsOutputDTOBase { }

    [FunctionOutput]
    public class DecimalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint8", "", 1)]
        public virtual byte ReturnValue1 { get; set; }
    }

    public partial class HashPairOutputDTO : HashPairOutputDTOBase { }

    [FunctionOutput]
    public class HashPairOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class NameOutputDTO : NameOutputDTOBase { }

    [FunctionOutput]
    public class NameOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class RootMerkleDropOutputDTO : RootMerkleDropOutputDTOBase { }

    [FunctionOutput]
    public class RootMerkleDropOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class RootMerklePaymentOutputDTO : RootMerklePaymentOutputDTOBase { }

    [FunctionOutput]
    public class RootMerklePaymentOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class SymbolOutputDTO : SymbolOutputDTOBase { }

    [FunctionOutput]
    public class SymbolOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class TotalSupplyOutputDTO : TotalSupplyOutputDTOBase { }

    [FunctionOutput]
    public class TotalSupplyOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }





    public partial class VerifyClaimOutputDTO : VerifyClaimOutputDTOBase { }

    [FunctionOutput]
    public class VerifyClaimOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "valid", 1)]
        public virtual bool Valid { get; set; }
    }

    public partial class VerifyPaymentIncludedOutputDTO : VerifyPaymentIncludedOutputDTOBase { }

    [FunctionOutput]
    public class VerifyPaymentIncludedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "valid", 1)]
        public virtual bool Valid { get; set; }
    }
}
