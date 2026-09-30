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
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator.ContractDefinition
{


    public partial class ECDSASessionValidatorDeployment : ECDSASessionValidatorDeploymentBase
    {
        public ECDSASessionValidatorDeployment() : base(BYTECODE) { }
        public ECDSASessionValidatorDeployment(string byteCode) : base(byteCode) { }
    }

    public class ECDSASessionValidatorDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557610401908161001a8239f35b5f80fdfe60806040526004361015610011575f80fd5b5f3560e01c80636d61fe701461010f5780638a91b0e31461010f578063940d3840146100a2578063d60b347f146100745763ecd0596114610050575f80fd5b346100705760203660031901126100705760206040516007600435148152f35b5f80fd5b34610070576020366003190112610070576004356001600160a01b0381160361007057602060405160018152f35b346100705760603660031901126100705760243567ffffffffffffffff8111610070576100d3903690600401610114565b6044359067ffffffffffffffff8211610070576020926100fa610105933690600401610114565b9290916004356101e1565b6040519015158152f35b610142565b9181601f840112156100705782359167ffffffffffffffff8311610070576020838186019501011161007057565b346100705760203660031901126100705760043567ffffffffffffffff811161007057610173903690600401610114565b005b92919267ffffffffffffffff82116101cd5760405191601f8101601f19908116603f0116830167ffffffffffffffff8111848210176101cd57604052829481845281830111610070578281602093845f960137010152565b634e487b7160e01b5f52604160045260245ffd5b929091936014810361028157601411610070573560601c928361021a61021161020b368587610175565b86610290565b909291926102ca565b6001600160a01b031614610278576102656102119261026b947f19457468657265756d205369676e6564204d6573736167653a0a3332000000005f52601c52603c5f20923691610175565b90610290565b6001600160a01b03161490565b50505050600190565b63378e602160e11b5f5260045ffd5b81519190604183036102c0576102b99250602082015190606060408401519301515f1a9061033e565b9192909190565b50505f9160029190565b600481101561032a57806102dc575050565b600181036102f35763f645eedf60e01b5f5260045ffd5b6002810361030e575063fce698f760e01b5f5260045260245ffd5b6003146103185750565b6335e2f38360e21b5f5260045260245ffd5b634e487b7160e01b5f52602160045260245ffd5b91907f7fffffffffffffffffffffffffffffff5d576e7357a4501ddfe92f46681b20a084116103c0579160209360809260ff5f9560405194855216868401526040830152606082015282805260015afa156103b5575f516001600160a01b038116156103ab57905f905f90565b505f906001905f90565b6040513d5f823e3d90fd5b5050505f916003919056fea2646970667358221220b10aac0763bff73104f2dbc38347b89a1ea1ff874d380ae7fa615246fce98a1364736f6c634300081c0033";
        public ECDSASessionValidatorDeploymentBase() : base(BYTECODE) { }
        public ECDSASessionValidatorDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class IsInitializedFunction : IsInitializedFunctionBase { }

    [Function("isInitialized", "bool")]
    public class IsInitializedFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class IsModuleTypeFunction : IsModuleTypeFunctionBase { }

    [Function("isModuleType", "bool")]
    public class IsModuleTypeFunctionBase : FunctionMessage
    {
        [Parameter("uint256", "moduleTypeId", 1)]
        public virtual BigInteger ModuleTypeId { get; set; }
    }

    public partial class OnInstallFunction : OnInstallFunctionBase { }

    [Function("onInstall")]
    public class OnInstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class OnUninstallFunction : OnUninstallFunctionBase { }

    [Function("onUninstall")]
    public class OnUninstallFunctionBase : FunctionMessage
    {
        [Parameter("bytes", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class ValidateSignatureWithDataFunction : ValidateSignatureWithDataFunctionBase { }

    [Function("validateSignatureWithData", "bool")]
    public class ValidateSignatureWithDataFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "hash", 1)]
        public virtual byte[] Hash { get; set; }
        [Parameter("bytes", "sig", 2)]
        public virtual byte[] Sig { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
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





    public partial class ValidateSignatureWithDataOutputDTO : ValidateSignatureWithDataOutputDTOBase { }

    [FunctionOutput]
    public class ValidateSignatureWithDataOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "validSig", 1)]
        public virtual bool ValidSig { get; set; }
    }

    public partial class AlreadyInitializedError : AlreadyInitializedErrorBase { }

    [Error("AlreadyInitialized")]
    public class AlreadyInitializedErrorBase : IErrorDTO
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
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

    public partial class InvalidSessionKeyDataError : InvalidSessionKeyDataErrorBase { }
    [Error("InvalidSessionKeyData")]
    public class InvalidSessionKeyDataErrorBase : IErrorDTO
    {
    }

    public partial class NotInitializedError : NotInitializedErrorBase { }

    [Error("NotInitialized")]
    public class NotInitializedErrorBase : IErrorDTO
    {
        [Parameter("address", "smartAccount", 1)]
        public virtual string SmartAccount { get; set; }
    }
}
