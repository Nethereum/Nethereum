using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition;

namespace Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition
{


    public partial class SimpleAuthorityDeployment : SimpleAuthorityDeploymentBase
    {
        public SimpleAuthorityDeployment() : base(BYTECODE) { }
        public SimpleAuthorityDeployment(string byteCode) : base(byteCode) { }
    }

    public class SimpleAuthorityDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x6080604052348015600e575f5ffd5b506040516107e63803806107e6833981016040819052602b916097565b6001600160a01b03811660745760405162461bcd60e51b815260206004820152600d60248201526c24b73b30b634b21037bbb732b960991b604482015260640160405180910390fd5b5f80546001600160a01b0319166001600160a01b039290921691909117905560c2565b5f6020828403121560a6575f5ffd5b81516001600160a01b038116811460bb575f5ffd5b9392505050565b610717806100cf5f395ff3fe608060405234801561000f575f5ffd5b506004361061009b575f3560e01c806397f988dc1161006357806397f988dc1461012c5780639ddbe2f114610154578063ab1e991214610181578063df20f0d1146101bb578063f2fde38b146101ce575f5ffd5b806315564d531461009f5780631e553d9b146100b45780634f4697a9146100c7578063700bcefb146100ef5780638da5cb5b14610102575b5f5ffd5b6100b26100ad366004610656565b6101e1565b005b6100b26100c2366004610656565b6102ef565b6100da6100d5366004610656565b6103ed565b60405190151581526020015b60405180910390f35b6100da6100fd366004610656565b610432565b5f54610114906001600160a01b031681565b6040516001600160a01b0390911681526020016100e6565b61011461013a366004610687565b60016020525f90815260409020546001600160a01b031681565b6100da610162366004610656565b600260209081525f928352604080842090915290825290205460ff1681565b6100da61018f366004610656565b67ffffffffffffffff919091165f908152600160205260409020546001600160a01b0390811691161490565b6100b26101c9366004610656565b610495565b6100b26101dc3660046106a0565b610549565b67ffffffffffffffff82165f908152600160205260409020546001600160a01b031633148061021957505f546001600160a01b031633145b61023e5760405162461bcd60e51b8152600401610235906106b9565b60405180910390fd5b6001600160a01b0381166102875760405162461bcd60e51b815260206004820152601060248201526f24b73b30b634b21037b832b930ba37b960811b6044820152606401610235565b67ffffffffffffffff82165f8181526001602052604080822080546001600160a01b038681166001600160a01b0319831681179093559251921693909284927fa14c3c2631346c9075676a4d133b2d62b6773ca9c07e03d1821b8113f6fbcd129190a4505050565b67ffffffffffffffff82165f908152600160205260409020546001600160a01b031633148061032757505f546001600160a01b031633145b6103435760405162461bcd60e51b8152600401610235906106b9565b6001600160a01b03811661038a5760405162461bcd60e51b815260206004820152600e60248201526d24b73b30b634b210383937bb32b960911b6044820152606401610235565b67ffffffffffffffff82165f8181526002602090815260408083206001600160a01b0386168085529252808320805460ff19166001179055519092917f7fca8ed1e0f130c06e0520c83b1fdde29109e5655b71d25bd0b622275d2438c991a35050565b67ffffffffffffffff82165f908152600160205260408120546001600160a01b038381169116148061042b57505f546001600160a01b038381169116145b9392505050565b67ffffffffffffffff82165f908152600160205260408120546001600160a01b038381169116148061042b57505067ffffffffffffffff919091165f9081526002602090815260408083206001600160a01b039094168352929052205460ff1690565b67ffffffffffffffff82165f908152600160205260409020546001600160a01b03163314806104cd57505f546001600160a01b031633145b6104e95760405162461bcd60e51b8152600401610235906106b9565b67ffffffffffffffff82165f8181526002602090815260408083206001600160a01b0386168085529252808320805460ff19169055519092917f325ccedbdaad579626ae32d649029c76d0217ade5e4847aa43ae07a0272c82be91a35050565b5f546001600160a01b0316331461058f5760405162461bcd60e51b815260206004820152600a60248201526927b7363c9037bbb732b960b11b6044820152606401610235565b6001600160a01b0381166105d55760405162461bcd60e51b815260206004820152600d60248201526c24b73b30b634b21037bbb732b960991b6044820152606401610235565b5f80546001600160a01b038381166001600160a01b0319831681178455604051919092169283917f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e09190a35050565b803567ffffffffffffffff8116811461063b575f5ffd5b919050565b80356001600160a01b038116811461063b575f5ffd5b5f5f60408385031215610667575f5ffd5b61067083610624565b915061067e60208401610640565b90509250929050565b5f60208284031215610697575f5ffd5b61042b82610624565b5f602082840312156106b0575f5ffd5b61042b82610640565b6020808252600e908201526d139bdd08185d5d1a1bdc9a5e995960921b60408201526060019056fea264697066735822122027c2622de28de7e0239a173c51eb970b01e0fd3be017b72154c76b96c969acaf64736f6c634300081c0033";
        public SimpleAuthorityDeploymentBase() : base(BYTECODE) { }
        public SimpleAuthorityDeploymentBase(string byteCode) : base(byteCode) { }
        [Parameter("address", "_owner", 1)]
        public virtual string Owner { get; set; }
    }

    public partial class AuthorizeProverFunction : AuthorizeProverFunctionBase { }

    [Function("authorizeProver")]
    public class AuthorizeProverFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "prover", 2)]
        public virtual string Prover { get; set; }
    }

    public partial class AuthorizedProversFunction : AuthorizedProversFunctionBase { }

    [Function("authorizedProvers", "bool")]
    public class AuthorizedProversFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
        [Parameter("address", "", 2)]
        public virtual string ReturnValue2 { get; set; }
    }

    public partial class CanManageChainFunction : CanManageChainFunctionBase { }

    [Function("canManageChain", "bool")]
    public class CanManageChainFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "caller", 2)]
        public virtual string Caller { get; set; }
    }

    public partial class CanProveFunction : CanProveFunctionBase { }

    [Function("canProve", "bool")]
    public class CanProveFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "caller", 2)]
        public virtual string Caller { get; set; }
    }

    public partial class CanSubmitAnchorFunction : CanSubmitAnchorFunctionBase { }

    [Function("canSubmitAnchor", "bool")]
    public class CanSubmitAnchorFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "caller", 2)]
        public virtual string Caller { get; set; }
    }

    public partial class OperatorsFunction : OperatorsFunctionBase { }

    [Function("operators", "address")]
    public class OperatorsFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "", 1)]
        public virtual ulong ReturnValue1 { get; set; }
    }

    public partial class OwnerFunction : OwnerFunctionBase { }

    [Function("owner", "address")]
    public class OwnerFunctionBase : FunctionMessage
    {

    }

    public partial class RevokeProverFunction : RevokeProverFunctionBase { }

    [Function("revokeProver")]
    public class RevokeProverFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "prover", 2)]
        public virtual string Prover { get; set; }
    }

    public partial class SetOperatorFunction : SetOperatorFunctionBase { }

    [Function("setOperator")]
    public class SetOperatorFunctionBase : FunctionMessage
    {
        [Parameter("uint64", "chainId", 1)]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "newOperator", 2)]
        public virtual string NewOperator { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }



    public partial class AuthorizedProversOutputDTO : AuthorizedProversOutputDTOBase { }

    [FunctionOutput]
    public class AuthorizedProversOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class CanManageChainOutputDTO : CanManageChainOutputDTOBase { }

    [FunctionOutput]
    public class CanManageChainOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class CanProveOutputDTO : CanProveOutputDTOBase { }

    [FunctionOutput]
    public class CanProveOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class CanSubmitAnchorOutputDTO : CanSubmitAnchorOutputDTOBase { }

    [FunctionOutput]
    public class CanSubmitAnchorOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class OperatorsOutputDTO : OperatorsOutputDTOBase { }

    [FunctionOutput]
    public class OperatorsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class OwnerOutputDTO : OwnerOutputDTOBase { }

    [FunctionOutput]
    public class OwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }







    public partial class OperatorSetEventDTO : OperatorSetEventDTOBase { }

    [Event("OperatorSet")]
    public class OperatorSetEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "oldOperator", 2, true )]
        public virtual string OldOperator { get; set; }
        [Parameter("address", "newOperator", 3, true )]
        public virtual string NewOperator { get; set; }
    }

    public partial class OwnershipTransferredEventDTO : OwnershipTransferredEventDTOBase { }

    [Event("OwnershipTransferred")]
    public class OwnershipTransferredEventDTOBase : IEventDTO
    {
        [Parameter("address", "oldOwner", 1, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class ProverAuthorizedEventDTO : ProverAuthorizedEventDTOBase { }

    [Event("ProverAuthorized")]
    public class ProverAuthorizedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "prover", 2, true )]
        public virtual string Prover { get; set; }
    }

    public partial class ProverRevokedEventDTO : ProverRevokedEventDTOBase { }

    [Event("ProverRevoked")]
    public class ProverRevokedEventDTOBase : IEventDTO
    {
        [Parameter("uint64", "chainId", 1, true )]
        public virtual ulong ChainId { get; set; }
        [Parameter("address", "prover", 2, true )]
        public virtual string Prover { get; set; }
    }
}
