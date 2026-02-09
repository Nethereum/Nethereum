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
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition
{


    public partial class ECDSAValidatorDeployment : ECDSAValidatorDeploymentBase
    {
        public ECDSAValidatorDeployment() : base(BYTECODE) { }
        public ECDSAValidatorDeployment(string byteCode) : base(byteCode) { }
    }

    public class ECDSAValidatorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610717908161001a8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f3560e01c908163022914a714610380575080636d61fe70146102c75780638a91b0e314610276578063970032031461022a578063d60b347f146101e9578063ecd05961146101c9578063f2fde38b1461011d578063f551e2ee146100c25763fa5441611461007f575f80fd5b346100be5760203660031901126100be576001600160a01b036100a06103be565b165f525f602052602060018060a01b0360405f205416604051908152f35b5f80fd5b346100be5760603660031901126100be576100db6103be565b5060443567ffffffffffffffff81116100be5761010a61010160209236906004016103d4565b90602435610517565b6040516001600160e01b03199091168152f35b346100be5760203660031901126100be576001600160a01b0361013e6103be565b1680156101ba57335f908152602081905260409020546001600160a01b0316156101ab57335f525f60205260405f20816bffffffffffffffffffffffff60a01b825416179055337f342827c97908e5e2f71151c08502a66d44b6f758e3ac2f1de95f02eb95f0a7355f80a3005b6321c4e35760e21b5f5260045ffd5b6349e27cff60e01b5f5260045ffd5b346100be5760203660031901126100be5760206040516001600435148152f35b346100be5760203660031901126100be576001600160a01b0361020a6103be565b165f525f602052602060018060a01b0360405f2054161515604051908152f35b346100be5760403660031901126100be5760043567ffffffffffffffff81116100be5761012060031982360301126100be5761026e6020916024359060040161046e565b604051908152f35b346100be5760203660031901126100be5760043567ffffffffffffffff81116100be576102a79036906004016103d4565b5050335f90815260208190526040902080546001600160a01b0319169055005b346100be5760203660031901126100be5760043567ffffffffffffffff81116100be576102f89036906004016103d4565b335f908152602081905260409020546001600160a01b0316610372576014116100be573560601c80156101ba57335f81815260208190526040812080546001600160a01b0319166001600160a01b0385161790557f342827c97908e5e2f71151c08502a66d44b6f758e3ac2f1de95f02eb95f0a7359080a3005b62dc149f60e41b5f5260045ffd5b346100be5760203660031901126100be576020906001600160a01b036103a46103be565b165f90815280835260409020546001600160a01b03168152f35b600435906001600160a01b03821682036100be57565b9181601f840112156100be5782359167ffffffffffffffff83116100be57602083818601950101116100be57565b92919267ffffffffffffffff821161045a5760405191601f8101601f19908116603f0116830167ffffffffffffffff81118482101761045a576040528294818452818301116100be578281602093845f960137010152565b634e487b7160e01b5f52604160045260245ffd5b9081356001600160a01b038116908190036100be575f908152602081905260409020546001600160a01b031691821561050f5761010081013590601e19813603018212156100be57019081359167ffffffffffffffff83116100be576020019082360382136100be576104e96104ef926104f8943691610402565b906105a6565b909291926105e0565b6001600160a01b03161461050b57600190565b5f90565b505050600190565b90335f525f60205260018060a01b0360405f205416928315610595576104e96104ef92610570947f19457468657265756d205369676e6564204d6573736167653a0a3332000000005f52601c52603c5f20923691610402565b6001600160a01b03161461058a576001600160e01b031990565b630b135d3f60e11b90565b506001600160e01b03199392505050565b81519190604183036105d6576105cf9250602082015190606060408401519301515f1a90610654565b9192909190565b50505f9160029190565b600481101561064057806105f2575050565b600181036106095763f645eedf60e01b5f5260045ffd5b60028103610624575063fce698f760e01b5f5260045260245ffd5b60031461062e5750565b6335e2f38360e21b5f5260045260245ffd5b634e487b7160e01b5f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a084116106d6579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa156106cb575f516001600160a01b038116156106c157905f905f90565b505f906001905f90565b6040513d5f823e3d90fd5b5050505f916003919056fea26469706673582212205914b1f5ffe872689c0197cac69528ff1786ad17849522e59de5be04d7046edc64736f6c634300081c0033";
        public ECDSAValidatorDeploymentBase() : base(BYTECODE) { }
        public ECDSAValidatorDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class GetOwnerFunction : GetOwnerFunctionBase { }

    [Function("getOwner", "address")]
    public class GetOwnerFunctionBase : FunctionMessage
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class IsModuleTypeFunction : IsModuleTypeFunctionBase { }

    [Function("isModuleType", "bool")]
    public class IsModuleTypeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
    }

    public partial class IsValidSignatureWithSenderFunction : IsValidSignatureWithSenderFunctionBase { }

    [Function("isValidSignatureWithSender", "bytes4")]
    public class IsValidSignatureWithSenderFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("bytes32", "hash", 2)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "signature", 3)]
        public virtual byte[] Signature { get; set; }
    }

    public partial class OnInstallFunction : OnInstallFunctionBase { }

    [Function("onInstall")]
    public class OnInstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "data", 1)]
        public virtual byte[] Data { get; set; }
    }

    public partial class OnUninstallFunction : OnUninstallFunctionBase { }

    [Function("onUninstall")]
    public class OnUninstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OwnersFunction : OwnersFunctionBase { }

    [Function("owners", "address")]
    public class OwnersFunctionBase : FunctionMessage
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }

    public partial class TransferOwnershipFunction : TransferOwnershipFunctionBase { }

    [Function("transferOwnership")]
    public class TransferOwnershipFunctionBase : FunctionMessage
    {
        [Parameter("address", "newOwner", 1)]
        public virtual string NewOwner { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "userOp", 1)]
        public virtual PackedUserOperation UserOp { get; set; }
        [Parameter("bytes32", "userOpHash", 2)]
        public virtual byte[] UserOpHash { get; set; }
    }

    public partial class GetOwnerOutputDTO : GetOwnerOutputDTOBase { }

    [FunctionOutput]
    public class GetOwnerOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsInitializedOutputDTO : IsInitializedOutputDTOBase { }

    [FunctionOutput]
    public class IsInitializedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsModuleTypeOutputDTO : IsModuleTypeOutputDTOBase { }

    [FunctionOutput]
    public class IsModuleTypeOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class IsValidSignatureWithSenderOutputDTO : IsValidSignatureWithSenderOutputDTOBase { }

    [FunctionOutput]
    public class IsValidSignatureWithSenderOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes4", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }





    public partial class OwnersOutputDTO : OwnersOutputDTOBase { }

    [FunctionOutput]
    public class OwnersOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "owner", 1)]
        public virtual string Owner { get; set; }
    }



    public partial class ValidateUserOpOutputDTO : ValidateUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidateUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "validationData", 1)]
        public virtual BigInteger ValidationData { get; set; }
    }

    public partial class OwnerSetEventDTO : OwnerSetEventDTOBase { }

    [Event("OwnerSet")]
    public class OwnerSetEventDTOBase : IEventDTO
    {
        [Parameter("address", "smartAccount", 1, true )]
        public virtual string SmartAccount { get; set; }
        [Parameter("address", "owner", 2, true )]
        public virtual string Owner { get; set; }
    }

    public partial class AlreadyInitializedError : AlreadyInitializedErrorBase { }
    [Error("AlreadyInitialized")]
    public class AlreadyInitializedErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureError : ECDSAInvalidSignatureErrorBase { }
    [Error("ECDSAInvalidSignature")]
    public class ECDSAInvalidSignatureErrorBase : IErrorDTO
    {
    }

    public partial class ECDSAInvalidSignatureLengthError : ECDSAInvalidSignatureLengthErrorBase { }

    [Error("ECDSAInvalidSignatureLength")]
    public class ECDSAInvalidSignatureLengthErrorBase : IErrorDTO
    {
        [Parameter("uint256", "length", 1)]
        public virtual BigInteger Length { get; set; }
    }

    public partial class ECDSAInvalidSignatureSError : ECDSAInvalidSignatureSErrorBase { }

    [Error("ECDSAInvalidSignatureS")]
    public class ECDSAInvalidSignatureSErrorBase : IErrorDTO
    {
        [Parameter("bytes32", "s", 1)]
        public virtual byte[] S { get; set; }
    }

    public partial class InvalidOwnerError : InvalidOwnerErrorBase { }
    [Error("InvalidOwner")]
    public class InvalidOwnerErrorBase : IErrorDTO
    {
    }

    public partial class NotInitializedError : NotInitializedErrorBase { }
    [Error("NotInitialized")]
    public class NotInitializedErrorBase : IErrorDTO
    {
    }
}
