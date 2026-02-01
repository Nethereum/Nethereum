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
using Nethereum.AccountAbstraction.Contracts.Modules.SocialRecoveryModule.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Modules.SocialRecoveryModule.ContractDefinition
{


    public partial class SocialRecoveryModuleDeployment : SocialRecoveryModuleDeploymentBase
    {
        public SocialRecoveryModuleDeployment() : base(BYTECODE) { }
        public SocialRecoveryModuleDeployment(string byteCode) : base(byteCode) { }
    }

    public class SocialRecoveryModuleDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE = "0x60808060405234601557611575908161001a8239f35b5f80fdfe6080806040526004361015610012575f80fd5b5f905f3560e01c90816301ffc9a71461125657508063113bc887146111fd57806323cdc3f1146111b457806337fbbe7d146101f95780634509c76f14611191578063484cc1d514610f8557806354fd4d5014610f6a5780635a5c98a214610f4d5780635ea3ec7614610e7a578063631fec7f14610d345780637897716814610ce65780637abed90514610af857806391100bb814610ad45780639700320314610a915780639b27a90e146108e55780639be5253a146108b25780639d87999014610821578063a1308f27146107e6578063a219b4041461077b578063a52d92cf14610465578063a9da7cde14610415578063c684521014610236578063cb60ee24146101fe578063d6cb4188146101f95763f18858ab14610131575f80fd5b346101f65760203660031901126101f6576001600160a01b036101526112a9565b1681528060205260408120604051908160208254918281520190819285526020852090855b8181106101d7575050508261018d91038361142d565b604051928392602084019060208552518091526040840192915b8181106101b5575050500390f35b82516001600160a01b03168452859450602093840193909201916001016101a7565b82546001600160a01b0316845260209093019260019283019201610177565b80fd5b6112d5565b50346101f65760203660031901126101f6576020906040906001600160a01b036102266112a9565b1681528083522054604051908152f35b50346101f65760403660031901126101f6576102506112a9565b6102586112bf565b906001600160a01b031633819003610406576001600160a01b0382169182156103bd57604051638da5cb5b60e01b8152602081600481865afa9081156103fb5785916103cc575b506001600160a01b031683146103bd5781845283602052604084209060028201845f528060205260ff60405f2054166103ae57845f5260205260405f20600160ff1982541617905581546801000000000000000081101561039a576004929161031182600161033594018555846114c5565b81546001600160a01b0393841660039290921b91821b9390911b1916919091179055565b8054845f526001820160205260405f205560038101805415610390575b5001805415610384575b507fbc3292102fa77e083913064b282926717cdfaede4d35f553d66366c0a3da755a8380a380f35b6202a30090555f61035c565b603c90555f610352565b634e487b7160e01b86526041600452602486fd5b63fecca77f60e01b8652600486fd5b63a6c1146b60e01b8452600484fd5b6103ee915060203d6020116103f4575b6103e6818361142d565b81019061144f565b5f61029f565b503d6103dc565b6040513d87823e3d90fd5b63f3f6425d60e01b8352600483fd5b50346101f65760203660031901126101f6576001600160a01b036104376112a9565b168152602081905260409020600401548015610459576020905b604051908152f35b5060206202a300610451565b50346101f65760403660031901126101f65761047f6112a9565b906104886112bf565b6001600160a01b03831680835260208381526040808520335f908152600290910190925290205490919060ff161561076c576001600160a01b03811693841561075d57828452600160205260408420600181019160ff83541660058110156107495760011461073a57848652856020526040862080541561072b57600401544201804211610717579067ffffffffffffffff61052b9216946003840154916114ee565b9461058763ffffffff604051610540816113fd565b89815260208101879052600160408201819052606090910152600160e01b60e09190911b1667ffffffffffffffff60a01b60a087901b166001600160a01b038a1617178355565b50600160ff1983541617825560018060a01b0333165f526002810160205260405f20600160ff19825416179055845f52600260205260405f2090808203610655575b6020868089887f26b0d6874b9c851c03b9ce8bd0b10b36fc04b4d13aba011a50aa7388cbbc704b858a855f526003825260405f2060018060a01b0333165f52825260405f20600160ff19825416179055604051908152a460405160018152817f3d0571b622a9fdc24bd62a07aba85a8ce8758a12edc8a485679b8b19d0d5d5ec843393a3604051908152f35b805482546001600160a01b039091166001600160a01b0319821681178455825467ffffffffffffffff60a01b166001600160e01b031990921617178255919592939160ff916001916106be905482546001600160e01b03166001600160e01b0319909116178255565b0195541694600586101561070357805460ff19169095179094559290817f26b0d6874b9c851c03b9ce8bd0b10b36fc04b4d13aba011a50aa7388cbbc704b60206105c9565b634e487b7160e01b5f52602160045260245ffd5b634e487b7160e01b87526011600452602487fd5b636bb07db960e11b8752600487fd5b631c730b7360e31b8652600486fd5b634e487b7160e01b87526021600452602487fd5b632a52b3c360e11b8452600484fd5b636570ecab60e11b8352600483fd5b50346101f65760403660031901126101f6576107956112a9565b6001600160a01b0316602435338290036104065760207fdc9e5e7eb38f77984d9a899bfdc7ca9e531f85f450602723e2adb9a37cebf4bc91838552848252806004604087200155604051908152a280f35b50346101f657806003193601126101f65760206040517f4668cbcb66762334b75a9f0b77662fefcb9f0b1a99f300ab66023c2e46f6fadc8152f35b50346101f65760403660031901126101f65761083b6112a9565b6001600160a01b03166024353382900361040657801580156108a8575b6108995760207fd3005e99e9685e065a46f77dad8a1a1d43354338c5eec3e7f4757beb17fee12991838552848252806003604087200155604051908152a280f35b63aabd5a0960e01b8352600483fd5b5060648111610858565b50346101f65760203660031901126101f657602090600435815260048252604060018060a01b0391205416604051908152f35b50346101f65760403660031901126101f6576108ff6112a9565b6109076112bf565b6001600160a01b0390911690338290036104065781835282602052604083206002810160018060a01b0383165f528060205260ff60405f20541615610a82576001600160a01b0383165f90815260209182526040808220805460ff191690556001840192839052902054909190806109ae575b5050506001600160a01b0316907fee943cdb81826d5909c559c6b1ae6908fcaf2dbc16c4b730346736b486283e8b8380a380f35b8154808203610a14575b505080548015610a00575f1901906109d082826114c5565b81549060018060a01b039060031b1b191690555560018060a01b0382165f526020528260405f20555f808061097a565b634e487b7160e01b86526031600452602486fd5b5f19810190811161071757610a2990836114c5565b905460039190911b1c6001600160a01b03165f198201828111610a6e5781610311610a5492866114c5565b60018060a01b03165f528260205260405f20555f806109b8565b634e487b7160e01b88526011600452602488fd5b630dd824e760e01b8552600485fd5b50346101f65760403660031901126101f65760043567ffffffffffffffff8111610ad0576101209060031990360301126101f657602060405160018152f35b5080fd5b50346101f65760203660031901126101f6576020610451610af36112a9565b61147c565b5034610c88576020366003190112610c8857600435805f52600260205260405f2090600182019160ff83541660058110156107035760018114159081610cda575b50610ccb575467ffffffffffffffff8160a01c164210610cbc57610b5b61133a565b8160e01c10610cad57604051638da5cb5b60e01b8152906020826004815f5afa918215610c7d575f92610c8c575b508354600360ff1991821681179095555f805260016020527fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4a80549091169094179093557fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4c80546001600160a01b0390941693610c059061146e565b90555f3b15610c885760405163a6f9dae160e01b8152600481018490525f8160248183805af18015610c7d57610c68575b506001600160a01b0316907f6a4fa7238aa224d542b5741448b8017d6fe8dea8b8a4ad71502ee1058a5228b58480a480f35b610c759194505f9061142d565b5f925f610c36565b6040513d5f823e3d90fd5b5f80fd5b610ca691925060203d6020116103f4576103e6818361142d565b905f610b89565b6359fa4a9360e01b5f5260045ffd5b637c57798b60e11b5f5260045ffd5b63cbfae98d60e01b5f5260045ffd5b6002915014155f610b39565b34610c88576020366003190112610c88576001600160a01b03610d076112a9565b165f525f602052600360405f2001548015155f14610d2a57602090604051908152f35b506020603c610451565b34610c88576020366003190112610c8857600435805f526002602052600160405f200160ff81541660058110156107035760018114159081610e6e575b50610ccb57604051638da5cb5b60e01b81526020816004815f5afa908115610c7d575f91610e4f575b506001600160a01b03163303610e40578054600460ff1991821681179092555f805260016020527fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4a805490911690911790557fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4c8054610e189061146e565b90557f7d6dceb0ad9e1c7d903468b0d49a3151dcb4d9f5fb0c5d0940b3ee2a7a8fd5d15f80a2005b63f3f6425d60e01b5f5260045ffd5b610e68915060203d6020116103f4576103e6818361142d565b83610d9a565b60029150141583610d71565b34610c88576020366003190112610c88575f6060604051610e9a816113fd565b82815282602082015282604082015201526004355f52600260205260405f2060405190610ec6826113fd565b80549060018060a01b038216835260ff6001602085019267ffffffffffffffff8560a01c168452604086019460e01c855201541691606084019260058110156107035783526040805194516001600160a01b03168552915167ffffffffffffffff1660208501525163ffffffff169083015251906005821015610703576080916060820152f35b34610c88575f366003190112610c885760206040516202a3008152f35b34610c88575f366003190112610c8857602060405160018152f35b34610c88576020366003190112610c8857600435805f52600260205260405f2090600182019160ff835416600581101561070357600103610ccb57335f9081527fad3228b676f7d3cd4284a5443f17f1962b36e491b30a40b2405849e597ba5fb7602052604090205460ff1615611182575f82815260036020908152604080832033845290915290205460ff16611173575f8281526003602090815260408083203384529091529020805460ff19166001179055805460e01c63ffffffff811461115f5781546001600160e01b0319600192830160e090811b919091166001600160e01b03928316811785557fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb49805490931617909155335f8181527fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4b6020908152604091829020805460ff1916909517909455935493519390911c80845293909290917f3d0571b622a9fdc24bd62a07aba85a8ce8758a12edc8a485679b8b19d0d5d5ec91a361111361133a565b111561111b57005b8054600260ff1991821681179092555f805260016020527fa6eef7e35abe7026729641147f7915573c7e97b47efa546f5f6e3230263bcb4a80549091169091179055005b634e487b7160e01b5f52601160045260245ffd5b63080fc0bd60e11b5f5260045ffd5b636570ecab60e11b5f5260045ffd5b34610c88576020366003190112610c885760206104516111af6112a9565b6113ac565b34610c88576040366003190112610c88576111cd6112bf565b6004355f52600360205260405f209060018060a01b03165f52602052602060ff60405f2054166040519015158152f35b34610c88576040366003190112610c88576112166112a9565b61121e6112bf565b9060018060a01b03165f525f602052600260405f20019060018060a01b03165f52602052602060ff60405f2054166040519015158152f35b34610c88576020366003190112610c88576004359063ffffffff60e01b8216809203610c8857602091634101631360e11b8114908115611298575b5015158152f35b632903475760e21b14905083611291565b600435906001600160a01b0382168203610c8857565b602435906001600160a01b0382168203610c8857565b34610c88576060366003190112610c88576004356001600160a01b0381168103610c88575060443567ffffffffffffffff8111610c885736602382011215610c885780600401359067ffffffffffffffff8211610c88573660248383010111610c8857005b5f8080526020527fad3228b676f7d3cd4284a5443f17f1962b36e491b30a40b2405849e597ba5fb580549081156113a65760030154801561139e575b8082029182040361115f57606481019081811161115f5760630190811161115f576064900490565b50603c611376565b50505f90565b6001600160a01b03165f90815260208190526040902080549081156113a65760030154801561139e578082029182040361115f57606481019081811161115f5760630190811161115f576064900490565b6080810190811067ffffffffffffffff82111761141957604052565b634e487b7160e01b5f52604160045260245ffd5b90601f8019910116810190811067ffffffffffffffff82111761141957604052565b90816020910312610c8857516001600160a01b0381168103610c885790565b5f19811461115f5760010190565b60018060a01b0381165f52600160205260405f209060ff6001830154166005811015610703576001036113a6576114c291600360018060a01b03825416910154916114ee565b90565b80548210156114da575f5260205f2001905f90565b634e487b7160e01b5f52603260045260245ffd5b916040519160208301936bffffffffffffffffffffffff199060601b1684526bffffffffffffffffffffffff199060601b16603483015260488201526048815261153960688261142d565b5190209056fea2646970667358221220c9b493fcc6e2cc4ba51a72e69760b725be14e3757cd24a5ecd405be1132a324c64736f6c634300081c0033";
        public SocialRecoveryModuleDeploymentBase() : base(BYTECODE) { }
        public SocialRecoveryModuleDeploymentBase(string byteCode) : base(byteCode) { }

    }

    public partial class AddGuardianFunction : AddGuardianFunctionBase { }

    [Function("addGuardian")]
    public class AddGuardianFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "guardian", 2)]
        public virtual string Guardian { get; set; }
    }

    public partial class ApproveRecoveryFunction : ApproveRecoveryFunctionBase { }

    [Function("approveRecovery")]
    public class ApproveRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class CancelRecoveryFunction : CancelRecoveryFunctionBase { }

    [Function("cancelRecovery")]
    public class CancelRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class ExecuteRecoveryFunction : ExecuteRecoveryFunctionBase { }

    [Function("executeRecovery")]
    public class ExecuteRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class GetAccountFromRecoveryIdFunction : GetAccountFromRecoveryIdFunctionBase { }

    [Function("getAccountFromRecoveryId", "address")]
    public class GetAccountFromRecoveryIdFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class GetCurrentRecoveryIdFunction : GetCurrentRecoveryIdFunctionBase { }

    [Function("getCurrentRecoveryId", "bytes32")]
    public class GetCurrentRecoveryIdFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetGuardianCountFunction : GetGuardianCountFunctionBase { }

    [Function("getGuardianCount", "uint256")]
    public class GetGuardianCountFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetGuardiansFunction : GetGuardiansFunctionBase { }

    [Function("getGuardians", "address[]")]
    public class GetGuardiansFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetRecoveryDelayFunction : GetRecoveryDelayFunctionBase { }

    [Function("getRecoveryDelay", "uint256")]
    public class GetRecoveryDelayFunctionBase : FunctionMessage
    {

    }

    public partial class GetRecoveryDelayForAccountFunction : GetRecoveryDelayForAccountFunctionBase { }

    [Function("getRecoveryDelayForAccount", "uint256")]
    public class GetRecoveryDelayForAccountFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetRecoveryRequestFunction : GetRecoveryRequestFunctionBase { }

    [Function("getRecoveryRequest", typeof(GetRecoveryRequestOutputDTO))]
    public class GetRecoveryRequestFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class GetRequiredApprovalsFunction : GetRequiredApprovalsFunctionBase { }

    [Function("getRequiredApprovals", "uint256")]
    public class GetRequiredApprovalsFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class GetThresholdFunction : GetThresholdFunctionBase { }

    [Function("getThreshold", "uint256")]
    public class GetThresholdFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
    }

    public partial class HasApprovedFunction : HasApprovedFunctionBase { }

    [Function("hasApproved", "bool")]
    public class HasApprovedFunctionBase : FunctionMessage
    {
        [Parameter("bytes32", "recoveryId", 1)]
        public virtual byte[] RecoveryId { get; set; }
        [Parameter("address", "guardian", 2)]
        public virtual string Guardian { get; set; }
    }

    public partial class InitiateRecoveryFunction : InitiateRecoveryFunctionBase { }

    [Function("initiateRecovery", "bytes32")]
    public class InitiateRecoveryFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "newOwner", 2)]
        public virtual string NewOwner { get; set; }
    }

    public partial class IsApproverFunction : IsApproverFunctionBase { }

    [Function("isApprover", "bool")]
    public class IsApproverFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "approver", 2)]
        public virtual string Approver { get; set; }
    }

    public partial class ModuleIdFunction : ModuleIdFunctionBase { }

    [Function("moduleId", "bytes32")]
    public class ModuleIdFunctionBase : FunctionMessage
    {

    }

    public partial class PostExecuteFunction : PostExecuteFunctionBase { }

    [Function("postExecute")]
    public class PostExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("bytes", "", 3)]
        public virtual byte[] ReturnValue3 { get; set; }
    }

    public partial class PreExecuteFunction : PreExecuteFunctionBase { }

    [Function("preExecute")]
    public class PreExecuteFunctionBase : FunctionMessage
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
        [Parameter("uint256", "", 2)]
        public virtual BigInteger ReturnValue2 { get; set; }
        [Parameter("bytes", "", 3)]
        public virtual byte[] ReturnValue3 { get; set; }
    }

    public partial class RemoveGuardianFunction : RemoveGuardianFunctionBase { }

    [Function("removeGuardian")]
    public class RemoveGuardianFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("address", "guardian", 2)]
        public virtual string Guardian { get; set; }
    }

    public partial class SetRecoveryDelayFunction : SetRecoveryDelayFunctionBase { }

    [Function("setRecoveryDelay")]
    public class SetRecoveryDelayFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "delay", 2)]
        public virtual BigInteger Delay { get; set; }
    }

    public partial class SetThresholdFunction : SetThresholdFunctionBase { }

    [Function("setThreshold")]
    public class SetThresholdFunctionBase : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public virtual string Account { get; set; }
        [Parameter("uint256", "threshold", 2)]
        public virtual BigInteger Threshold { get; set; }
    }

    public partial class SupportsInterfaceFunction : SupportsInterfaceFunctionBase { }

    [Function("supportsInterface", "bool")]
    public class SupportsInterfaceFunctionBase : FunctionMessage
    {
        [Parameter("bytes4", "interfaceId", 1)]
        public virtual byte[] InterfaceId { get; set; }
    }

    public partial class ValidateUserOpFunction : ValidateUserOpFunctionBase { }

    [Function("validateUserOp", "uint256")]
    public class ValidateUserOpFunctionBase : FunctionMessage
    {
        [Parameter("tuple", "", 1)]
        public virtual PackedUserOperation ReturnValue1 { get; set; }
        [Parameter("bytes32", "", 2)]
        public virtual byte[] ReturnValue2 { get; set; }
    }

    public partial class VersionFunction : VersionFunctionBase { }

    [Function("version", "uint256")]
    public class VersionFunctionBase : FunctionMessage
    {

    }









    public partial class GetAccountFromRecoveryIdOutputDTO : GetAccountFromRecoveryIdOutputDTOBase { }

    [FunctionOutput]
    public class GetAccountFromRecoveryIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GetCurrentRecoveryIdOutputDTO : GetCurrentRecoveryIdOutputDTOBase { }

    [FunctionOutput]
    public class GetCurrentRecoveryIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }

    public partial class GetGuardianCountOutputDTO : GetGuardianCountOutputDTOBase { }

    [FunctionOutput]
    public class GetGuardianCountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetGuardiansOutputDTO : GetGuardiansOutputDTOBase { }

    [FunctionOutput]
    public class GetGuardiansOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("address[]", "", 1)]
        public virtual List<string> ReturnValue1 { get; set; }
    }

    public partial class GetRecoveryDelayOutputDTO : GetRecoveryDelayOutputDTOBase { }

    [FunctionOutput]
    public class GetRecoveryDelayOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetRecoveryDelayForAccountOutputDTO : GetRecoveryDelayForAccountOutputDTOBase { }

    [FunctionOutput]
    public class GetRecoveryDelayForAccountOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetRecoveryRequestOutputDTO : GetRecoveryRequestOutputDTOBase { }

    [FunctionOutput]
    public class GetRecoveryRequestOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("tuple", "", 1)]
        public virtual RecoveryRequest ReturnValue1 { get; set; }
    }

    public partial class GetRequiredApprovalsOutputDTO : GetRequiredApprovalsOutputDTOBase { }

    [FunctionOutput]
    public class GetRequiredApprovalsOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GetThresholdOutputDTO : GetThresholdOutputDTOBase { }

    [FunctionOutput]
    public class GetThresholdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class HasApprovedOutputDTO : HasApprovedOutputDTOBase { }

    [FunctionOutput]
    public class HasApprovedOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }



    public partial class IsApproverOutputDTO : IsApproverOutputDTOBase { }

    [FunctionOutput]
    public class IsApproverOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ModuleIdOutputDTO : ModuleIdOutputDTOBase { }

    [FunctionOutput]
    public class ModuleIdOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bytes32", "", 1)]
        public virtual byte[] ReturnValue1 { get; set; }
    }











    public partial class SupportsInterfaceOutputDTO : SupportsInterfaceOutputDTOBase { }

    [FunctionOutput]
    public class SupportsInterfaceOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("bool", "", 1)]
        public virtual bool ReturnValue1 { get; set; }
    }

    public partial class ValidateUserOpOutputDTO : ValidateUserOpOutputDTOBase { }

    [FunctionOutput]
    public class ValidateUserOpOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class VersionOutputDTO : VersionOutputDTOBase { }

    [FunctionOutput]
    public class VersionOutputDTOBase : IFunctionOutputDTO 
    {
        [Parameter("uint256", "", 1)]
        public virtual BigInteger ReturnValue1 { get; set; }
    }

    public partial class GuardianAddedEventDTO : GuardianAddedEventDTOBase { }

    [Event("GuardianAdded")]
    public class GuardianAddedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "guardian", 2, true )]
        public virtual string Guardian { get; set; }
    }

    public partial class GuardianRemovedEventDTO : GuardianRemovedEventDTOBase { }

    [Event("GuardianRemoved")]
    public class GuardianRemovedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "guardian", 2, true )]
        public virtual string Guardian { get; set; }
    }

    public partial class RecoveryApprovedEventDTO : RecoveryApprovedEventDTOBase { }

    [Event("RecoveryApproved")]
    public class RecoveryApprovedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "recoveryId", 1, true )]
        public virtual byte[] RecoveryId { get; set; }
        [Parameter("address", "approver", 2, true )]
        public virtual string Approver { get; set; }
        [Parameter("uint32", "approvalCount", 3, false )]
        public virtual uint ApprovalCount { get; set; }
    }

    public partial class RecoveryCancelledEventDTO : RecoveryCancelledEventDTOBase { }

    [Event("RecoveryCancelled")]
    public class RecoveryCancelledEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "recoveryId", 1, true )]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class RecoveryDelayChangedEventDTO : RecoveryDelayChangedEventDTOBase { }

    [Event("RecoveryDelayChanged")]
    public class RecoveryDelayChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "newDelay", 2, false )]
        public virtual BigInteger NewDelay { get; set; }
    }

    public partial class RecoveryExecutedEventDTO : RecoveryExecutedEventDTOBase { }

    [Event("RecoveryExecuted")]
    public class RecoveryExecutedEventDTOBase : IEventDTO
    {
        [Parameter("bytes32", "recoveryId", 1, true )]
        public virtual byte[] RecoveryId { get; set; }
        [Parameter("address", "oldOwner", 2, true )]
        public virtual string OldOwner { get; set; }
        [Parameter("address", "newOwner", 3, true )]
        public virtual string NewOwner { get; set; }
    }

    public partial class RecoveryInitiatedEventDTO : RecoveryInitiatedEventDTOBase { }

    [Event("RecoveryInitiated")]
    public class RecoveryInitiatedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("address", "newOwner", 2, true )]
        public virtual string NewOwner { get; set; }
        [Parameter("uint64", "executeAfter", 3, false )]
        public virtual ulong ExecuteAfter { get; set; }
        [Parameter("bytes32", "recoveryId", 4, true )]
        public virtual byte[] RecoveryId { get; set; }
    }

    public partial class ThresholdChangedEventDTO : ThresholdChangedEventDTOBase { }

    [Event("ThresholdChanged")]
    public class ThresholdChangedEventDTOBase : IEventDTO
    {
        [Parameter("address", "account", 1, true )]
        public virtual string Account { get; set; }
        [Parameter("uint256", "newThreshold", 2, false )]
        public virtual BigInteger NewThreshold { get; set; }
    }

    public partial class AlreadyApprovedError : AlreadyApprovedErrorBase { }
    [Error("AlreadyApproved")]
    public class AlreadyApprovedErrorBase : IErrorDTO
    {
    }

    public partial class GuardianAlreadyExistsError : GuardianAlreadyExistsErrorBase { }
    [Error("GuardianAlreadyExists")]
    public class GuardianAlreadyExistsErrorBase : IErrorDTO
    {
    }

    public partial class GuardianNotExistsError : GuardianNotExistsErrorBase { }
    [Error("GuardianNotExists")]
    public class GuardianNotExistsErrorBase : IErrorDTO
    {
    }

    public partial class InvalidGuardianError : InvalidGuardianErrorBase { }
    [Error("InvalidGuardian")]
    public class InvalidGuardianErrorBase : IErrorDTO
    {
    }

    public partial class InvalidNewOwnerError : InvalidNewOwnerErrorBase { }
    [Error("InvalidNewOwner")]
    public class InvalidNewOwnerErrorBase : IErrorDTO
    {
    }

    public partial class InvalidThresholdError : InvalidThresholdErrorBase { }
    [Error("InvalidThreshold")]
    public class InvalidThresholdErrorBase : IErrorDTO
    {
    }

    public partial class NoRecoveryPendingError : NoRecoveryPendingErrorBase { }
    [Error("NoRecoveryPending")]
    public class NoRecoveryPendingErrorBase : IErrorDTO
    {
    }

    public partial class NotEnoughGuardiansError : NotEnoughGuardiansErrorBase { }
    [Error("NotEnoughGuardians")]
    public class NotEnoughGuardiansErrorBase : IErrorDTO
    {
    }

    public partial class OnlyAccountError : OnlyAccountErrorBase { }
    [Error("OnlyAccount")]
    public class OnlyAccountErrorBase : IErrorDTO
    {
    }

    public partial class OnlyGuardianError : OnlyGuardianErrorBase { }
    [Error("OnlyGuardian")]
    public class OnlyGuardianErrorBase : IErrorDTO
    {
    }

    public partial class RecoveryAlreadyPendingError : RecoveryAlreadyPendingErrorBase { }
    [Error("RecoveryAlreadyPending")]
    public class RecoveryAlreadyPendingErrorBase : IErrorDTO
    {
    }

    public partial class RecoveryNotReadyError : RecoveryNotReadyErrorBase { }
    [Error("RecoveryNotReady")]
    public class RecoveryNotReadyErrorBase : IErrorDTO
    {
    }

    public partial class ThresholdNotMetError : ThresholdNotMetErrorBase { }
    [Error("ThresholdNotMet")]
    public class ThresholdNotMetErrorBase : IErrorDTO
    {
    }
}
